using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// يكتشف ألعاب Steam المثبتة عبر قراءة سجل الويندوز لمعرفة مسار Steam،
/// ثم قراءة ملفات libraryfolders.vdf و appmanifest_*.acf مباشرة من القرص
/// (بدون الحاجة لأي API key أو اتصال إنترنت).
/// </summary>
public class SteamLibraryProvider : IGameLibraryProvider
{
    public string PlatformName => "Steam";

    public bool IsAvailable() => GetSteamPath() != null;

    public IEnumerable<GameInfo> ScanLibrary()
    {
        var steamPath = GetSteamPath();
        if (steamPath == null) yield break;

        var seenAppIds = new HashSet<string>();

        foreach (var libFolder in GetLibraryFolders(steamPath))
        {
            var steamAppsPath = Path.Combine(libFolder, "steamapps");
            if (!Directory.Exists(steamAppsPath)) continue;

            foreach (var manifestFile in Directory.GetFiles(steamAppsPath, "appmanifest_*.acf"))
            {
                var game = ParseManifest(manifestFile, steamAppsPath);
                if (game == null) continue;

                // نفس اللعبة ممكن يكون لها manifest متبقي بأكثر من مكتبة
                // (خصوصًا بعد نقل مكتبة Steam) — نعرضها مرة وحدة بس
                if (!seenAppIds.Add(game.Id)) continue;

                yield return game;
            }
        }
    }

    private static string? GetSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetLibraryFolders(string steamPath)
    {
        var result = new List<string> { NormalizePath(steamPath) };
        var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdfPath)) return result;

        var content = File.ReadAllText(vdfPath);
        var matches = Regex.Matches(content, "\"path\"\\s*\"([^\"]+)\"");
        foreach (Match m in matches)
        {
            var path = NormalizePath(m.Groups[1].Value.Replace(@"\\", @"\"));
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase)) result.Add(path);
        }
        return result;
    }

    private static string NormalizePath(string path) => path.TrimEnd('\\', '/');

    private static GameInfo? ParseManifest(string manifestPath, string steamAppsPath)
    {
        var content = File.ReadAllText(manifestPath);

        var idMatch = Regex.Match(content, "\"appid\"\\s*\"(\\d+)\"");
        var nameMatch = Regex.Match(content, "\"name\"\\s*\"([^\"]+)\"");
        var installDirMatch = Regex.Match(content, "\"installdir\"\\s*\"([^\"]+)\"");

        if (!nameMatch.Success || !installDirMatch.Success) return null;

        var installPath = Path.Combine(steamAppsPath, "common", installDirMatch.Groups[1].Value);
        var appId = idMatch.Success ? idMatch.Groups[1].Value : Guid.NewGuid().ToString();

        return new GameInfo
        {
            Id = appId,
            Name = nameMatch.Groups[1].Value,
            InstallPath = installPath,
            ExecutablePath = $"steam://rungameid/{appId}",
            Platform = "Steam",
            // غلاف عمودي من CDN ستيم العام، ما يحتاج أي مفتاح API
            CoverImageUrl = idMatch.Success
                ? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg"
                : null,
            IsInstalled = Directory.Exists(installPath)
        };
    }
}
