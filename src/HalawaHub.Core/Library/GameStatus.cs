using System.Text.Json;
using System.Text.RegularExpressions;
using HalawaHub.Core.Models;
using HalawaHub.Core.Sessions;
using Microsoft.Win32;

namespace HalawaHub.Core.Library;

[Flags]
public enum GameStatus
{
    None = 0,
    UpdateAvailable = 1,
    Updating = 2,
    NeedsRepair = 4,
    Running = 8,
    DriveNotConnected = 16
}

/// <summary>
/// بتات StateFlags في appmanifest_*.acf. المصدر: Lutris (docs/steam.rst وجدول
/// APP_STATE_FLAGS في lutris/util/steam/appmanifest.py)، ومطابقة لملفات حقيقية:
/// 4 = مثبتة، 6/38 = تحتاج تحديثًا، 1026 = تحديث بدأ (حسب أدلة تعديل appmanifest).
/// </summary>
[Flags]
public enum SteamAppState : long
{
    Uninstalled = 1,
    UpdateRequired = 2,
    FullyInstalled = 4,
    Encrypted = 8,
    Locked = 16,
    FilesMissing = 32,
    AppRunning = 64,
    FilesCorrupt = 128,
    UpdateRunning = 256,
    UpdatePaused = 512,
    UpdateStarted = 1024,
    Uninstalling = 2048,
    BackupRunning = 4096,
    Reconfiguring = 65536,
    Validating = 131072,
    AddingFiles = 262144,
    Preallocating = 524288,
    Downloading = 1048576,
    Staging = 2097152,
    Committing = 4194304,
    UpdateStopping = 8388608
}

public sealed record SteamManifestState(long StateFlags, string? BuildId, string? TargetBuildId);

/// <summary>
/// حالة كل لعبة من قراءات ملفات رخيصة فقط (بلا شبكة): يصلح للاستدعاء عند كل
/// تحديث للمكتبة وعند رجوع التركيز للنافذة. كل ما يخص النظام يُمرَّر كدوال للاختبار.
/// </summary>
public sealed class GameStatusProbe
{
    public Func<string, bool> DirExists { get; init; } = Directory.Exists;
    public Func<string, bool> FileExists { get; init; } = File.Exists;
    public Func<string, string?> ReadText { get; init; } = SafeRead;

    /// (يعمل، يتحدث) لكل لعبة Steam من سجل ويندوز/registry.vdf؛ null = لا نعرف
    public Func<string, (bool Running, bool Updating)?> SteamAppRegistry { get; init; } = ReadSteamAppRegistry;

    public Func<string?> EpicManifestsDir { get; init; } = () =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests")
            : null;

    public bool Windows { get; init; } = OperatingSystem.IsWindows();

    public static GameStatusProbe System { get; } = new();

    public GameStatus Probe(GameInfo game)
    {
        if (IsDriveMissing(game.InstallPath)) return GameStatus.DriveNotConnected;

        return game.Platform switch
        {
            "Steam" => ProbeSteam(game),
            "Epic Games" => ProbeEpic(game),
            _ => GameStatus.None
        };
    }

    public bool IsDriveMissing(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Path.IsPathRooted(installPath)) return false;
        if (Windows)
        {
            var root = Path.GetPathRoot(installPath);
            return !string.IsNullOrEmpty(root) && !root.StartsWith(@"\\", StringComparison.Ordinal) && !DirExists(root);
        }
        return LaunchPreflight.IsRemovableMount(installPath) is { } mount && !DirExists(mount);
    }

    // ===== Steam =====

    /// steamapps/common/&lt;dir&gt; → steamapps/appmanifest_&lt;id&gt;.acf
    public static string? SteamManifestPath(GameInfo game)
    {
        if (string.IsNullOrEmpty(game.InstallPath) || !SteamSessionTracker.IsValidAppId(game.Id)) return null;
        var common = Path.GetDirectoryName(game.InstallPath.TrimEnd('\\', '/'));
        var steamapps = common == null ? null : Path.GetDirectoryName(common);
        return steamapps == null ? null : Path.Combine(steamapps, $"appmanifest_{game.Id}.acf");
    }

    internal static SteamManifestState? ParseAcf(string content)
    {
        static string? Read(string text, string key)
        {
            var m = Regex.Match(text, "\"" + key + "\"\\s+\"(?<v>[^\"]*)\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["v"].Value : null;
        }

        var flags = Read(content, "StateFlags");
        if (flags == null || !long.TryParse(flags, out var value)) return null;
        return new SteamManifestState(value, Read(content, "buildid"), Read(content, "TargetBuildID"));
    }

    internal static GameStatus FromSteamState(SteamManifestState s)
    {
        var f = (SteamAppState)s.StateFlags;
        var status = GameStatus.None;

        var updateRequired = f.HasFlag(SteamAppState.UpdateRequired)
                             || (!string.IsNullOrEmpty(s.TargetBuildId) && s.TargetBuildId != "0"
                                 && !string.IsNullOrEmpty(s.BuildId) && s.TargetBuildId != s.BuildId);
        const SteamAppState busy = SteamAppState.UpdateRunning | SteamAppState.Downloading | SteamAppState.Staging
                                   | SteamAppState.Committing | SteamAppState.Preallocating | SteamAppState.Validating;

        if ((f & busy) != 0) status |= GameStatus.Updating;
        else if (updateRequired) status |= GameStatus.UpdateAvailable;

        // FilesMissing يرافق التحديث المعلّق في ملفات حقيقية (38 = 32+4+2)، والتحديث نفسه
        // يعيد الملفات، فلا نقول "تحتاج إصلاحًا" إلا حين لا يوجد تحديث مطلوب
        if (f.HasFlag(SteamAppState.FilesCorrupt) || (f.HasFlag(SteamAppState.FilesMissing) && !updateRequired))
            status |= GameStatus.NeedsRepair;

        if (f.HasFlag(SteamAppState.AppRunning)) status |= GameStatus.Running;
        return status;
    }

    private GameStatus ProbeSteam(GameInfo game)
    {
        var status = GameStatus.None;
        var manifest = SteamManifestPath(game);
        if (manifest != null && ReadText(manifest) is { } text && ParseAcf(text) is { } state)
            status = FromSteamState(state);

        if (SteamAppRegistry(game.Id) is { } reg)
        {
            if (reg.Running) status |= GameStatus.Running;
            if (reg.Updating)
            {
                status |= GameStatus.Updating;
                status &= ~GameStatus.UpdateAvailable;
            }
        }
        return status;
    }

    private static (bool Running, bool Updating)? ReadSteamAppRegistry(string appId)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.CurrentUser.OpenSubKey($@"Software\Valve\Steam\Apps\{appId}");
                if (key == null) return null;
                return (Convert.ToInt64(key.GetValue("Running") ?? 0) != 0, Convert.ToInt64(key.GetValue("Updating") ?? 0) != 0);
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var path in new[]
                     {
                         Path.Combine(home, ".steam", "registry.vdf"),
                         Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".steam", "registry.vdf")
                     })
            {
                if (SafeRead(path) is { } vdf && ParseRegistryVdfApp(vdf, appId) is { } r) return r;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Steam app registry read failed", ex);
        }
        return null;
    }

    /// قسم "Apps" { "&lt;id&gt;" { "Running" "0" "Updating" "0" } } في registry.vdf على لينكس
    internal static (bool Running, bool Updating)? ParseRegistryVdfApp(string vdf, string appId)
    {
        var m = Regex.Match(vdf, "\"" + Regex.Escape(appId) + "\"\\s*\\{(?<body>[^{}]*)\\}");
        if (!m.Success) return null;
        var body = m.Groups["body"].Value;
        bool Flag(string name)
        {
            var v = Regex.Match(body, "\"" + name + "\"\\s*\"(?<v>\\d+)\"", RegexOptions.IgnoreCase);
            return v.Success && v.Groups["v"].Value != "0";
        }
        return (Flag("Running"), Flag("Updating"));
    }

    // ===== Epic =====

    /// Epic لا يكتب "تحديث متوفر" محليًا (يحتاج تسجيل دخول)، لكنه يكتب حاجة الإصلاح
    private GameStatus ProbeEpic(GameInfo game)
    {
        var dir = EpicManifestsDir();
        if (string.IsNullOrEmpty(dir) || !DirExists(dir)) return GameStatus.None;

        string[] files;
        try { files = Directory.GetFiles(dir, "*.item"); }
        catch { return GameStatus.None; }

        foreach (var file in files)
        {
            if (ReadText(file) is not { } json) continue;
            var status = EpicStatusFromManifest(json, game.Id);
            if (status != null) return status.Value;
        }
        return GameStatus.None;
    }

    /// null إن لم يكن الملف لهذه اللعبة
    internal static GameStatus? EpicStatusFromManifest(string json, string appName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var name = root.TryGetProperty("AppName", out var an) ? an.GetString() : null;
            if (!string.Equals(name, appName, StringComparison.Ordinal)) return null;

            bool Flag(string p) => root.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.True;
            return Flag("bNeedsValidation") || Flag("bIsIncompleteInstall") ? GameStatus.NeedsRepair : GameStatus.None;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            return reader.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }
}
