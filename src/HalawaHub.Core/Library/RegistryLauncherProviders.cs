using System.Runtime.Versioning;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using Microsoft.Win32;

namespace HalawaHub.Core.Library;

/// مفتاح فرعي في سجل ويندوز كما يقرؤه المزود: اسمه وقيمه النصية
public sealed record RegistryEntry(string KeyName, IReadOnlyDictionary<string, string> Values)
{
    public string? Get(string name) => Values.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
}

/// <summary>
/// قراءة مفاتيح HKLM (32 و64 بت). كل المزودات هنا ويندوز فقط، وقراءة السجل تُمرَّر
/// كدالة حتى تُختبر المزودات بمدخلات منسوخة من سجل حقيقي على أي نظام.
/// </summary>
public static class WindowsRegistryReader
{
    public static List<RegistryEntry> SubKeys(string path)
    {
        var result = new List<RegistryEntry>();
        if (!OperatingSystem.IsWindows()) return result;
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try { ReadView(path, view, result); }
            catch (Exception ex) { Log.Error($"registry read failed: {path}", ex); }
        }
        return result;
    }

    [SupportedOSPlatform("windows")]
    private static void ReadView(string path, RegistryView view, List<RegistryEntry> result)
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var key = hklm.OpenSubKey(path);
        if (key == null) return;
        foreach (var name in key.GetSubKeyNames())
        {
            if (result.Any(r => string.Equals(r.KeyName, name, StringComparison.OrdinalIgnoreCase))) continue;
            using var sub = key.OpenSubKey(name);
            if (sub == null) continue;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in sub.GetValueNames())
                if (sub.GetValue(v) is string s) values[v] = s;
            result.Add(new RegistryEntry(name, values));
        }
    }

    internal static bool IsSteamCopy(string path) =>
        path.Replace('/', '\\').Contains(@"\steamapps\", StringComparison.OrdinalIgnoreCase);

    internal static string CleanName(string name) =>
        name.Replace("™", "").Replace("®", "").Replace("(x64)", "", StringComparison.OrdinalIgnoreCase).Replace("  ", " ").Trim();
}

/// <summary>
/// Ubisoft Connect: HKLM\SOFTWARE\(WOW6432Node\)Ubisoft\Launcher\Installs\&lt;id&gt; فيه InstallDir.
/// السجل لا يحفظ اسم اللعبة، فنأخذه من اسم مجلدها. التشغيل: uplay://launch/&lt;id&gt;/0.
/// (المسار مؤكد على جهاز فيه Ubisoft Connect؛ لا لعبة مثبتة هناك، فالتشغيل غير مجرَّب حيًّا.)
/// </summary>
public sealed class UbisoftLibraryProvider : IGameLibraryProvider
{
    public const string Platform = "Ubisoft";
    private const string InstallsKey = @"SOFTWARE\Ubisoft\Launcher\Installs";
    private readonly Func<string, List<RegistryEntry>> _read;
    private readonly Func<string, bool> _dirExists;

    public UbisoftLibraryProvider() : this(WindowsRegistryReader.SubKeys, Directory.Exists) { }

    internal UbisoftLibraryProvider(Func<string, List<RegistryEntry>> read, Func<string, bool> dirExists)
    {
        _read = read;
        _dirExists = dirExists;
    }

    public string PlatformName => Platform;
    public bool IsAvailable() => OperatingSystem.IsWindows() && _read(InstallsKey).Count > 0;

    public IEnumerable<GameInfo> ScanLibrary()
    {
        foreach (var e in _read(InstallsKey))
        {
            var dir = e.Get("InstallDir")?.TrimEnd('\\', '/');
            if (dir == null || !e.KeyName.All(char.IsAsciiDigit)) continue;
            var name = Path.GetFileName(dir.Replace('/', '\\').TrimEnd('\\').Split('\\').Last());
            if (string.IsNullOrEmpty(name)) continue;
            yield return new GameInfo
            {
                Id = e.KeyName, Name = name, Platform = Platform, InstallPath = dir,
                ExecutablePath = $"uplay://launch/{e.KeyName}/0", IsInstalled = _dirExists(dir)
            };
        }
    }
}

/// <summary>
/// EA app: HKLM\SOFTWARE\(WOW6432Node\)EA Games\&lt;اسم&gt; فيه "Install Dir" و DisplayName،
/// و Origin Games\&lt;offerId&gt; فيه DisplayName (نطابق بالاسم لنعرف رقم العرض).
/// التشغيل: origin2://game/launch?offerIds=&lt;id&gt;؛ بلا رقم عرض يُفتح EA app نفسه.
/// نسخ Steam من ألعاب EA (مسارها داخل steamapps) تُترك لمزود Steam حتى لا تتكرر.
/// </summary>
public sealed class EaLibraryProvider : IGameLibraryProvider
{
    public const string Platform = "EA";
    private const string GamesKey = @"SOFTWARE\EA Games";
    private const string OffersKey = @"SOFTWARE\Origin Games";
    private readonly Func<string, List<RegistryEntry>> _read;
    private readonly Func<string, bool> _dirExists;

    public EaLibraryProvider() : this(WindowsRegistryReader.SubKeys, Directory.Exists) { }

    internal EaLibraryProvider(Func<string, List<RegistryEntry>> read, Func<string, bool> dirExists)
    {
        _read = read;
        _dirExists = dirExists;
    }

    public string PlatformName => Platform;
    public bool IsAvailable() => OperatingSystem.IsWindows() && _read(GamesKey).Count > 0;

    public IEnumerable<GameInfo> ScanLibrary()
    {
        var offers = _read(OffersKey)
            .Where(o => o.Get("DisplayName") != null)
            .GroupBy(o => WindowsRegistryReader.CleanName(o.Get("DisplayName")!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().KeyName, StringComparer.OrdinalIgnoreCase);

        foreach (var e in _read(GamesKey))
        {
            var dir = e.Get("Install Dir")?.TrimEnd('\\', '/');
            if (dir == null || WindowsRegistryReader.IsSteamCopy(dir)) continue;
            var name = WindowsRegistryReader.CleanName(e.Get("DisplayName") ?? e.KeyName);
            if (name.Length == 0) continue;
            offers.TryGetValue(name, out var offerId);
            yield return new GameInfo
            {
                Id = offerId ?? e.KeyName, Name = name, Platform = Platform, InstallPath = dir,
                ExecutablePath = offerId != null ? $"origin2://game/launch?offerIds={offerId}" : "origin2://library/open",
                IsInstalled = _dirExists(dir)
            };
        }
    }
}

/// <summary>
/// Rockstar Games Launcher: HKLM\SOFTWARE\(WOW6432Node\)Rockstar Games\&lt;اللعبة&gt; فيه InstallFolder
/// لألعاب المشغّل نفسه (نسخ Steam وEpic تُكتب كـ InstallFolderSteam / InstallFolderEpic وتُترك
/// لمزوديها). التشغيل: Launcher.exe -launchTitleInFolder "&lt;المجلد&gt;".
/// </summary>
public sealed class RockstarLibraryProvider : IGameLibraryProvider
{
    public const string Platform = "Rockstar";
    private const string RootKey = @"SOFTWARE\Rockstar Games";
    private static readonly string[] NotGames = { "Launcher", "Rockstar Games Social Club", "Steam", "Epic" };
    private readonly Func<string, List<RegistryEntry>> _read;
    private readonly Func<string, bool> _dirExists;

    public RockstarLibraryProvider() : this(WindowsRegistryReader.SubKeys, Directory.Exists) { }

    internal RockstarLibraryProvider(Func<string, List<RegistryEntry>> read, Func<string, bool> dirExists)
    {
        _read = read;
        _dirExists = dirExists;
    }

    public string PlatformName => Platform;

    public bool IsAvailable() => OperatingSystem.IsWindows() && LauncherExe(_read(RootKey)) != null;

    private static string? LauncherExe(List<RegistryEntry> entries)
    {
        var dir = entries.FirstOrDefault(e => e.KeyName == "Launcher")?.Get("InstallFolder");
        return dir == null ? null : Path.Combine(dir, "Launcher.exe");
    }

    public IEnumerable<GameInfo> ScanLibrary()
    {
        var entries = _read(RootKey);
        var launcher = LauncherExe(entries);
        if (launcher == null) yield break;

        foreach (var e in entries)
        {
            if (NotGames.Contains(e.KeyName, StringComparer.OrdinalIgnoreCase)) continue;
            var dir = e.Get("InstallFolder")?.TrimEnd('\\', '/');
            if (dir == null || WindowsRegistryReader.IsSteamCopy(dir)) continue;
            yield return new GameInfo
            {
                Id = e.KeyName, Name = e.KeyName, Platform = Platform, InstallPath = dir,
                ExecutablePath = launcher, LaunchArguments = $"-launchTitleInFolder \"{dir}\"",
                IsInstalled = _dirExists(dir)
            };
        }
    }
}
