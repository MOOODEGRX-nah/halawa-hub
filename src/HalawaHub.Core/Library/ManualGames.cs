using System.Text.Json;
using System.Text.RegularExpressions;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// لعبة أضافها المستخدم بنفسه (manual-games.json)
public sealed class ManualGameRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// ما يُشغَّل: ملف تنفيذي، أو .lnk/.url (ويندوز)، أو AppImage، أو رابط من ملف .desktop
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";

    /// مجلد اللعبة للتتبع وفتح الموقع؛ فارغ إن لم نعرفه (رابط مثلًا)
    public string InstallDir { get; set; } = "";
    public DateTime AddedUtc { get; set; }
}

/// <summary>
/// إضافة لعبة يدويًا: يحوّل الملف الذي اختاره المستخدم إلى ما يُشغَّل وإلى مجلد يُتتبع.
/// .lnk يُحل لهدفه عبر WScript.Shell (ويندوز)، .url يُقرأ منه URL=، و.desktop يُقرأ منه
/// Exec= (بدون رموز %f/%u) و Path=.
/// </summary>
public static class ManualGames
{
    public const string Platform = "Manual";

    public static string DefaultPath => AppPaths.InData("manual-games.json");

    public static List<ManualGameRecord> Load(string path) =>
        AtomicFile.ReadWithBackup(path, json => JsonSerializer.Deserialize<List<ManualGameRecord>>(json), "manual-games.json")
        ?? new List<ManualGameRecord>();

    public static void Save(string path, List<ManualGameRecord> records) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));

    public static readonly string[] WindowsExtensions = { "*.exe", "*.lnk", "*.url", "*.bat" };
    public static readonly string[] LinuxExtensions = { "*.desktop", "*.AppImage", "*.appimage", "*.sh", "*" };

    /// null إذا كان الملف غير مدعوم أو تالفًا
    public static ManualGameRecord? FromFile(string file, Func<string, string?>? resolveLnk = null)
    {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return null;
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var name = Path.GetFileNameWithoutExtension(file);
        var record = new ManualGameRecord { Id = Guid.NewGuid().ToString("N"), Name = name, AddedUtc = DateTime.UtcNow };

        switch (ext)
        {
            case ".lnk":
                var target = (resolveLnk ?? ResolveLnk)(file);
                record.Target = file; // يُشغَّل الاختصار نفسه (يحفظ وسائطه ومجلد عمله)
                record.InstallDir = !string.IsNullOrEmpty(target) && File.Exists(target) ? Path.GetDirectoryName(target) ?? "" : "";
                return record;

            case ".url":
                var url = ReadIniValue(File.ReadAllText(file), "URL");
                if (string.IsNullOrEmpty(url)) return null;
                record.Target = url;
                return record;

            case ".desktop":
                return FromDesktopFile(File.ReadAllText(file), record);

            default:
                record.Target = file;
                record.InstallDir = Path.GetDirectoryName(file) ?? "";
                return record;
        }
    }

    internal static ManualGameRecord? FromDesktopFile(string content, ManualGameRecord record)
    {
        var name = ReadIniValue(content, "Name");
        var exec = ReadIniValue(content, "Exec");
        if (string.IsNullOrEmpty(exec)) return null;

        // رموز الحقول (%f %F %u %U %i %c %k) تخص مدير الملفات، لا تُمرَّر
        exec = Regex.Replace(exec, "\\s*%[fFuUickdDnNvm]", "").Trim();
        var (program, args) = SplitCommand(exec);
        if (string.IsNullOrEmpty(program)) return null;

        if (!string.IsNullOrEmpty(name)) record.Name = name;
        record.Target = program;
        record.Arguments = args;
        var path = ReadIniValue(content, "Path");
        record.InstallDir = !string.IsNullOrEmpty(path) ? path
            : Path.IsPathRooted(program) ? Path.GetDirectoryName(program) ?? "" : "";
        return record;
    }

    /// أول قيمة للمفتاح في ملف بصيغة INI (.url و.desktop)
    internal static string? ReadIniValue(string content, string key)
    {
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(key + "=", StringComparison.Ordinal)) return line[(key.Length + 1)..].Trim();
        }
        return null;
    }

    /// "/opt/x/game" --a "b c" → (/opt/x/game, --a "b c")
    internal static (string Program, string Args) SplitCommand(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            if (end < 0) return (command.Trim('"'), "");
            return (command[1..end], command[(end + 1)..].Trim());
        }
        var space = command.IndexOf(' ');
        return space < 0 ? (command, "") : (command[..space], command[(space + 1)..].Trim());
    }

    public static GameInfo ToGame(ManualGameRecord r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Platform = Platform,
        InstallPath = r.InstallDir,
        ExecutablePath = r.Target,
        LaunchArguments = string.IsNullOrWhiteSpace(r.Arguments) ? null : r.Arguments,
        IsInstalled = true
    };

    private static string? ResolveLnk(string lnk)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return null;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic shortcut = shell.CreateShortcut(lnk);
                return (string?)shortcut.TargetPath;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"could not resolve shortcut {lnk}", ex);
            return null;
        }
    }
}

/// مزود "أضفتها بنفسك": يقرأ manual-games.json
public sealed class ManualGamesProvider : IGameLibraryProvider
{
    private readonly string _path;

    public ManualGamesProvider(string? path = null) => _path = path ?? ManualGames.DefaultPath;

    public string PlatformName => ManualGames.Platform;

    public bool IsAvailable() => File.Exists(_path);

    public IEnumerable<GameInfo> ScanLibrary() => ManualGames.Load(_path).Select(ManualGames.ToGame).ToList();
}
