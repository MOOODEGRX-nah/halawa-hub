using System.Text.RegularExpressions;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using Microsoft.Win32;

namespace HalawaHub.Core.Sessions;

public enum LaunchProblem
{
    None,

    /// القرص الذي عليه اللعبة غير موصول — لا نشغّل
    DriveNotConnected,

    /// مجلد اللعبة أو ملفها غير موجود — لا نشغّل
    GameFilesMissing,

    /// المشغّل (Steam، Epic، Riot Client…) غير مثبت أو رابطه غير مسجّل — لا نشغّل
    LauncherNotFound,

    /// Steam يعمل بلا حساب: نشغّل (Steam يعرض نافذة الدخول) وننبّه المستخدم
    SteamNotSignedIn
}

/// <summary>
/// فحوص سريعة قبل الضغط على "تشغيل"، حتى يرى المستخدم سببًا واضحًا بدل ألا يحدث شيء.
/// كل الاعتماديات على النظام (أقراص، ملفات، سجل ويندوز) تُمرَّر كدوال لتُختبر.
/// </summary>
public sealed class LaunchPreflight
{
    public Func<string, bool> DirExists { get; init; } = Directory.Exists;
    public Func<string, bool> FileExists { get; init; } = File.Exists;
    public Func<string, bool> ProtocolRegistered { get; init; } = IsProtocolRegistered;
    public Func<bool?> SteamSignedIn { get; init; } = IsSteamSignedIn;
    public bool Windows { get; init; } = OperatingSystem.IsWindows();

    public static LaunchPreflight System { get; } = new();

    /// المشكلة الأولى التي تمنع التشغيل أو تستحق تنبيهًا، و"المسار" الذي يخصها لعرضه
    public (LaunchProblem Problem, string Detail) Check(GameInfo game)
    {
        var install = game.InstallPath?.Trim() ?? "";
        if (install.Length > 0 && IsRooted(install, Windows) && !DirExists(install))
        {
            if (Windows)
            {
                var root = RootOf(install, Windows);
                if (!string.IsNullOrEmpty(root) && !root.StartsWith(@"\\", StringComparison.Ordinal) && !DirExists(root))
                    return (LaunchProblem.DriveNotConnected, root.TrimEnd('\\'));
            }
            else if (IsRemovableMount(install) is { } mount && !DirExists(mount))
            {
                return (LaunchProblem.DriveNotConnected, mount);
            }
            return (LaunchProblem.GameFilesMissing, install);
        }

        var exe = game.ExecutablePath?.Trim() ?? "";
        if (UrlScheme(exe) is { } scheme)
        {
            if (!ProtocolRegistered(scheme))
                return (LaunchProblem.LauncherNotFound, LauncherName(game, scheme));
        }
        else if (exe.Length > 0 && IsRooted(exe, Windows) && !FileExists(exe))
        {
            // Riot: الملف التنفيذي هو Riot Client، لا اللعبة
            return game.Platform == "Riot Games"
                ? (LaunchProblem.LauncherNotFound, "Riot Client")
                : (LaunchProblem.GameFilesMissing, exe);
        }

        if (game.Platform == "Steam" && SteamSignedIn() == false)
            return (LaunchProblem.SteamNotSignedIn, "Steam");

        return (LaunchProblem.None, "");
    }

    /// مسار مطلق حسب نظام المسار نفسه لا النظام الذي نعمل عليه (Path.IsPathRooted لا يفهم C: على لينكس)
    public static bool IsRooted(string path, bool windows) => windows
        ? (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/') || path.StartsWith(@"\\", StringComparison.Ordinal)
        : path.StartsWith('/');

    /// "X:\" لمسار ويندوز بحرف قرص؛ مسار الشبكة يرجع كما هو (لا يُفحص كقرص)
    public static string? RootOf(string path, bool windows) =>
        !windows ? "/" : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\" : path.Length >= 3 ? path[..2] + "\\" : null;

    public static bool BlocksLaunch(LaunchProblem p) =>
        p is LaunchProblem.DriveNotConnected or LaunchProblem.GameFilesMissing or LaunchProblem.LauncherNotFound;

    /// steam://rungameid/1 → steam ؛ lutris:rungameid/1 → lutris ؛ C:\x.exe → null
    public static string? UrlScheme(string exe)
    {
        var m = Regex.Match(exe, @"^(?<s>[A-Za-z][A-Za-z0-9+.\-]+):(?!\\)");
        if (!m.Success) return null;
        var s = m.Groups["s"].Value;
        return s.Length > 1 ? s.ToLowerInvariant() : null;
    }

    private static string LauncherName(GameInfo game, string scheme) => scheme switch
    {
        "steam" => "Steam",
        "com.epicgames.launcher" => "Epic Games Launcher",
        "heroic" => "Heroic",
        "lutris" => "Lutris",
        "goggalaxy" => "GOG Galaxy",
        _ => game.Platform
    };

    /// /run/media/me/Games/x → /run/media/me/Games
    internal static string? IsRemovableMount(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 && parts[0] == "run" && parts[1] == "media") return "/" + string.Join('/', parts.Take(4));
        if (parts.Length >= 2 && parts[0] is "media" or "mnt")
        {
            // /media/me/Disk أو /media/Disk
            var depth = parts[0] == "media" && parts.Length >= 3 ? 3 : 2;
            return "/" + string.Join('/', parts.Take(depth));
        }
        return null;
    }

    /// <summary>
    /// ويندوز: الرابط مسجّل إذا كان له مفتاح في HKCR فيه "URL Protocol". بدونه يفتح
    /// ويندوز نافذة "ابحث عن تطبيق" بلا أي خطأ. لينكس: يتولاه مراقب xdg-open الحالي.
    /// </summary>
    public static bool IsProtocolRegistered(string scheme)
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(scheme);
            return key?.GetValue("URL Protocol") != null;
        }
        catch (Exception ex)
        {
            Log.Error($"protocol check failed for {scheme}", ex);
            return true;
        }
    }

    /// <summary>
    /// null = لا نعرف (Steam مغلق: سيفتح ويسجّل الدخول تلقائيًا غالبًا).
    /// ويندوز: HKCU\Software\Valve\Steam\ActiveProcess (pid ≠ 0 و ActiveUser = 0 → غير مسجّل).
    /// لينكس: نفس القيم في ~/.steam/registry.vdf.
    /// </summary>
    public static bool? IsSteamSignedIn()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
                if (key == null) return null;
                var pid = Convert.ToInt64(key.GetValue("pid") ?? 0);
                var user = Convert.ToInt64(key.GetValue("ActiveUser") ?? 0);
                return SignedInFrom(pid, user);
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var path in new[]
                     {
                         Path.Combine(home, ".steam", "registry.vdf"),
                         Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".steam", "registry.vdf")
                     })
            {
                if (!File.Exists(path)) continue;
                var (pid, user) = ParseRegistryVdf(File.ReadAllText(path));
                if (pid != null) return SignedInFrom(pid.Value, user ?? 0);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Steam sign-in check failed", ex);
        }
        return null;
    }

    internal static bool? SignedInFrom(long steamPid, long activeUser) =>
        steamPid == 0 ? null : activeUser != 0;

    /// يقرأ "pid" و"ActiveUser" من قسم ActiveProcess في registry.vdf
    internal static (long? Pid, long? ActiveUser) ParseRegistryVdf(string content)
    {
        var section = Regex.Match(content, "\"ActiveProcess\"\\s*\\{(?<body>[^}]*)\\}", RegexOptions.IgnoreCase);
        if (!section.Success) return (null, null);
        var body = section.Groups["body"].Value;

        static long? Read(string body, string name)
        {
            var m = Regex.Match(body, "\"" + name + "\"\\s*\"(?<v>\\d+)\"", RegexOptions.IgnoreCase);
            return m.Success && long.TryParse(m.Groups["v"].Value, out var v) ? v : null;
        }
        return (Read(body, "pid"), Read(body, "ActiveUser"));
    }
}
