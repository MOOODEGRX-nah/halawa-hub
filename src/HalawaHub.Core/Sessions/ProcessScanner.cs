using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace HalawaHub.Core.Sessions;

/// عملية واحدة كما يراها Halawa: مسار ملفها التنفيذي ووسائطها (لينكس فقط، لأن ألعاب
/// Wine/Proton يظهر مسار اللعبة في وسائط wine لا في مسار الملف التنفيذي)
public sealed record ProcessEntry(int Pid, string? ExePath, IReadOnlyList<string> Args)
{
    public string Name => ExePath == null ? "" : Path.GetFileName(ExePath.Replace('\\', '/'));
}

public interface IProcessScanner
{
    IReadOnlyList<ProcessEntry> Snapshot();

    bool IsAlive(int pid);

    /// null إن تعذّر معرفته (عملية محمية مثلًا)
    DateTime? StartTimeUtc(int pid);
}

/// <summary>
/// قائمة العمليات الحقيقية. ويندوز: QueryFullProcessImageName بصلاحية قراءة محدودة
/// (تعمل حتى مع عمليات 32/64 بت والعمليات المرفوعة غالبًا، بلا استثناءات MainModule).
/// لينكس: /proc/&lt;pid&gt;/exe و /proc/&lt;pid&gt;/cmdline.
/// </summary>
public sealed class SystemProcessScanner : IProcessScanner
{
    public static SystemProcessScanner Instance { get; } = new();

    public IReadOnlyList<ProcessEntry> Snapshot() =>
        OperatingSystem.IsWindows() ? SnapshotWindows() : SnapshotProc();

    public bool IsAlive(int pid)
    {
        if (pid <= 0) return false;
        if (!OperatingSystem.IsWindows()) return Directory.Exists($"/proc/{pid}");

        Process p;
        try
        {
            p = Process.GetProcessById(pid);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }

        using (p)
        {
            try
            {
                return !p.HasExited;
            }
            catch
            {
                // ألعاب بمضاد غش تعمل بصلاحيات أعلى فيُرفض سؤالها — موجودة إذن
                return true;
            }
        }
    }

    public DateTime? StartTimeUtc(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.StartTime.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    private static List<ProcessEntry> SnapshotWindows()
    {
        var result = new List<ProcessEntry>();
        Process[] all;
        try
        {
            all = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            Log.Error("process snapshot failed", ex);
            return result;
        }

        var buffer = new StringBuilder(1024);
        foreach (var p in all)
        {
            using (p)
            {
                int pid;
                try { pid = p.Id; } catch { continue; }
                if (pid <= 4) continue;
                result.Add(new ProcessEntry(pid, QueryImagePath(pid, buffer), Array.Empty<string>()));
            }
        }
        return result;
    }

    private const uint ProcessQueryLimitedInformation = 0x1000;

    private static string? QueryImagePath(int pid, StringBuilder buffer)
    {
        if (!OperatingSystem.IsWindows()) return null;
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            buffer.Clear();
            buffer.EnsureCapacity(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static List<ProcessEntry> SnapshotProc()
    {
        var result = new List<ProcessEntry>();
        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories("/proc");
        }
        catch (Exception ex)
        {
            Log.Error("process snapshot failed", ex);
            return result;
        }

        foreach (var dir in dirs)
        {
            if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue;

            string? exe = null;
            try { exe = new FileInfo(Path.Combine(dir, "exe")).LinkTarget; } catch { /* عملية مستخدم آخر أو انتهت */ }

            IReadOnlyList<string> args = Array.Empty<string>();
            try
            {
                var raw = File.ReadAllBytes(Path.Combine(dir, "cmdline"));
                if (raw.Length > 0)
                    args = Encoding.UTF8.GetString(raw).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }
            catch { /* انتهت أثناء القراءة */ }

            if (exe == null && args.Count == 0) continue;
            result.Add(new ProcessEntry(pid, exe, args));
        }
        return result;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}

/// <summary>
/// هل تقع عملية داخل مجلد لعبة؟ يقارن مسار الملف التنفيذي، وعلى لينكس أيضًا كل وسيط
/// يشبه مسارًا (wine يكتب مسار اللعبة كـ Z:\home\... في وسائطه).
/// </summary>
public static class GameFolderMatcher
{
    /// عمليات مساعدة تبقى أحيانًا بعد خروج اللعبة أو تعمل وحدها، فلا تُعدّ لعبًا
    private static readonly string[] IgnoredNameParts =
    {
        "crashhandler", "crashpad", "crashreport", "crashsender", "bugsplat", "werfault"
    };

    public static bool IsIgnoredHelper(string name)
    {
        var lower = name.ToLowerInvariant();
        return IgnoredNameParts.Any(lower.Contains);
    }

    /// windows: نظام المسار (للاختبار على نظام آخر)؛ GetFullPath فقط لمسارات النظام الحالي
    public static string? NormalizeDir(string? dir, bool? windows = null)
    {
        if (string.IsNullOrWhiteSpace(dir)) return null;
        try
        {
            var trimmedDir = dir.Trim();
            var full = (windows ?? OperatingSystem.IsWindows()) == OperatingSystem.IsWindows()
                ? Path.GetFullPath(trimmedDir)
                : trimmedDir;
            return full.TrimEnd('\\', '/');
        }
        catch
        {
            return null;
        }
    }

    public static bool IsInside(ProcessEntry p, string normalizedDir, bool windows)
    {
        if (p.ExePath != null && PathIsInside(p.ExePath, normalizedDir, windows)) return true;
        if (windows) return false;

        foreach (var arg in p.Args)
        {
            var candidate = FromWinePath(arg);
            if (candidate != null && PathIsInside(candidate, normalizedDir, windows)) return true;
        }
        return false;
    }

    /// Z:\home\me\Games\X\x.exe → /home/me/Games/X/x.exe ؛ المسارات العادية كما هي
    internal static string? FromWinePath(string arg)
    {
        if (arg.Length > 3 && (arg[0] == 'Z' || arg[0] == 'z') && arg[1] == ':' && (arg[2] == '\\' || arg[2] == '/'))
            return arg[2..].Replace('\\', '/');
        return arg.StartsWith('/') ? arg : null;
    }

    public static bool PathIsInside(string path, string normalizedDir, bool windows)
    {
        var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (windows)
        {
            path = path.Replace('/', '\\');
            normalizedDir = normalizedDir.Replace('/', '\\');
            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path[4..];
        }
        if (path.Length <= normalizedDir.Length) return false;
        if (!path.StartsWith(normalizedDir, comparison)) return false;
        var sep = path[normalizedDir.Length];
        return sep == '\\' || sep == '/';
    }

    /// <summary>
    /// مجلد لا يصلح للتتبع: جذر قرص، أو مجلد نظام عام، أو مجلد قصير جدًا
    /// (كل عملية فيه ستُحسب لعبًا، وهذا "غامض" فلا نحتسب شيئًا).
    /// </summary>
    public static bool IsTooBroad(string normalizedDir, bool windows)
    {
        var trimmed = normalizedDir.TrimEnd('\\', '/');
        var parts = trimmed.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return true;

        var comparer = windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var broad = new List<string>();
        void Add(Environment.SpecialFolder f)
        {
            try
            {
                var p = Environment.GetFolderPath(f);
                if (!string.IsNullOrEmpty(p)) broad.Add(p.TrimEnd('\\', '/'));
            }
            catch { /* غير متاح على هذا النظام */ }
        }
        Add(Environment.SpecialFolder.ProgramFiles);
        Add(Environment.SpecialFolder.ProgramFilesX86);
        Add(Environment.SpecialFolder.Windows);
        Add(Environment.SpecialFolder.System);
        Add(Environment.SpecialFolder.UserProfile);
        Add(Environment.SpecialFolder.LocalApplicationData);
        Add(Environment.SpecialFolder.ApplicationData);
        Add(Environment.SpecialFolder.MyDocuments);
        Add(Environment.SpecialFolder.Desktop);
        if (windows) broad.Add(Path.Combine(Path.GetPathRoot(trimmed) ?? "C:\\", "Program Files", "WindowsApps"));
        else broad.AddRange(new[] { "/usr", "/usr/bin", "/usr/lib", "/usr/share", "/opt", "/home", "/run/media", "/mnt", "/media" });

        return broad.Any(b => comparer.Equals(b, trimmed));
    }
}
