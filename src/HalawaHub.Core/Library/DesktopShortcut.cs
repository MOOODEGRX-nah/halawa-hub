using System.Diagnostics;
using System.Text;
using HalawaHub.Core.Models;

namespace HalawaHub.Core.Library;

/// <summary>
/// اختصار للعبة على سطح المكتب يشغّلها مباشرة (بنفس رابط/ملف Halawa).
/// ويندوز: رابط (steam://…) → ملف .url؛ ملف تنفيذي → .lnk عبر WScript.Shell (يحفظ الوسائط
/// التي تحتاجها Riot وXbox). لينكس: ملف .desktop في مجلد سطح المكتب (XDG_DESKTOP_DIR)
/// قابل للتنفيذ، ويُعلَّم "موثوقًا" عبر gio إن وُجد (GNOME؛ KDE يكفيه بت التنفيذ).
/// </summary>
public static class DesktopShortcut
{
    public static string Create(GameInfo game, string displayName, string? desktopDir = null)
    {
        var dir = desktopDir ?? DesktopDirectory();
        Directory.CreateDirectory(dir);
        var safe = SafeFileName(displayName);
        var target = game.ExecutablePath ?? "";
        var isUrl = HalawaHub.Core.Sessions.LaunchPreflight.UrlScheme(target) != null;

        if (OperatingSystem.IsWindows())
        {
            if (isUrl)
            {
                var path = Unique(Path.Combine(dir, safe + ".url"));
                File.WriteAllText(path, UrlFileContent(target));
                return path;
            }
            var lnk = Unique(Path.Combine(dir, safe + ".lnk"));
            CreateLnk(lnk, target, game.LaunchArguments, game.InstallPath);
            return lnk;
        }

        var desktop = Unique(Path.Combine(dir, safe + ".desktop"));
        File.WriteAllText(desktop, DesktopFileContent(displayName, target, game.LaunchArguments, isUrl));
        File.SetUnixFileMode(desktop, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                      | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                      | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        TryMarkTrusted(desktop);
        return desktop;
    }

    internal static string UrlFileContent(string url) => "[InternetShortcut]\r\nURL=" + url + "\r\n";

    internal static string DesktopFileContent(string name, string target, string? args, bool isUrl)
    {
        var exec = isUrl
            ? "xdg-open " + Quote(target)
            : Quote(target) + (string.IsNullOrWhiteSpace(args) ? "" : " " + args);
        var sb = new StringBuilder();
        sb.Append("[Desktop Entry]\n");
        sb.Append("Type=Application\n");
        sb.Append("Name=").Append(name.Replace('\n', ' ')).Append('\n');
        sb.Append("Exec=").Append(exec).Append('\n');
        sb.Append("Icon=applications-games\n");
        sb.Append("Terminal=false\n");
        sb.Append("Categories=Game;\n");
        return sb.ToString();
    }

    /// قواعد اقتباس Exec في مواصفة desktop-entry: علامتا تنصيص، و\ قبل " ` $ \
    private static string Quote(string s)
    {
        var escaped = new StringBuilder();
        foreach (var c in s)
        {
            if (c is '"' or '`' or '$' or '\\') escaped.Append('\\');
            escaped.Append(c);
        }
        return "\"" + escaped + "\"";
    }

    internal static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' }).ToHashSet();
        var cleaned = new string(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? ' ' : c).ToArray()).Trim().TrimEnd('.');
        return cleaned.Length == 0 ? "Game" : cleaned;
    }

    private static string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 2; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    public static string DesktopDirectory()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var dirsFile = Path.Combine(string.IsNullOrEmpty(config) ? Path.Combine(home, ".config") : config, "user-dirs.dirs");
        try
        {
            if (File.Exists(dirsFile) && ParseXdgDesktopDir(File.ReadAllText(dirsFile), home) is { } d) return d;
        }
        catch { /* الافتراضي أدناه */ }
        return Path.Combine(home, "Desktop");
    }

    /// XDG_DESKTOP_DIR="$HOME/سطح المكتب"
    internal static string? ParseXdgDesktopDir(string content, string home)
    {
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("XDG_DESKTOP_DIR=", StringComparison.Ordinal)) continue;
            var value = line["XDG_DESKTOP_DIR=".Length..].Trim().Trim('"');
            return value.Replace("$HOME", home);
        }
        return null;
    }

    private static void CreateLnk(string lnk, string target, string? args, string? workingDir)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not available");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(lnk);
            shortcut.TargetPath = target;
            if (!string.IsNullOrWhiteSpace(args)) shortcut.Arguments = args;
            var wd = !string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir) ? workingDir : Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(wd)) shortcut.WorkingDirectory = wd;
            if (File.Exists(target)) shortcut.IconLocation = target + ",0";
            shortcut.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void TryMarkTrusted(string desktopFile)
    {
        try
        {
            var psi = new ProcessStartInfo("gio") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var a in new[] { "set", desktopFile, "metadata::trusted", "true" }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
        }
        catch
        {
            // gio غير موجود (KDE غالبًا): بت التنفيذ يكفي
        }
    }
}
