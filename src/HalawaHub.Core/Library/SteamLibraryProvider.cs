using System.IO;
using System.Text.RegularExpressions;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using Microsoft.Win32;

namespace HalawaHub.Core.Library;

public class SteamLibraryProvider : IGameLibraryProvider
{
    public string PlatformName => "Steam";

    public bool IsAvailable() => GetSteamPath() != null;

    public IEnumerable<GameInfo> ScanLibrary()
    {
        var steamPath = GetSteamPath();
        if (steamPath == null) yield break;

        var seenAppIds = new HashSet<string>();
        List<string> libraryFolders;
        try
        {
            libraryFolders = GetLibraryFolders(steamPath);
        }
        catch (Exception ex)
        {
            // VDF مقفل أو تالف — نكتفي بمجلد Steam الرئيسي
            Log.Error("Steam: failed to read libraryfolders.vdf", ex);
            libraryFolders = new List<string> { NormalizePath(steamPath) };
        }

        foreach (var libFolder in libraryFolders)
        {
            var steamAppsPath = Path.Combine(libFolder, "steamapps");
            if (!Directory.Exists(steamAppsPath)) continue;

            string[] manifestFiles;
            try
            {
                manifestFiles = Directory.GetFiles(steamAppsPath, "appmanifest_*.acf");
            }
            catch (Exception ex)
            {
                // مجلد steamapps غير قابل للقراءة — نتخطاه
                Log.Error($"Steam: cannot enumerate {steamAppsPath}", ex);
                continue;
            }

            foreach (var manifestFile in manifestFiles)
            {
                GameInfo? game = null;
                try
                {
                    game = ParseManifest(manifestFile, steamAppsPath);
                }
                catch (Exception ex)
                {
                    // manifest مقفل/تالف — نتخطاه ولا نكسر بقية المكتبة
                    Log.Error($"Steam: manifest failed {Path.GetFileName(manifestFile)}", ex);
                }

                if (game == null) continue;
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

        string content;
        try
        {
            content = File.ReadAllText(vdfPath);
        }
        catch (Exception ex)
        {
            Log.Error($"Steam: vdf unreadable at {vdfPath}", ex);
            return result;
        }

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
        string content;
        try
        {
            content = File.ReadAllText(manifestPath);
        }
        catch (Exception ex)
        {
            Log.Error($"Steam: manifest unreadable {manifestPath}", ex);
            return null;
        }

        var idMatch = Regex.Match(content, "\"appid\"\\s*\"(\\d+)\"");
        var nameMatch = Regex.Match(content, "\"name\"\\s*\"([^\"]+)\"");
        var installDirMatch = Regex.Match(content, "\"installdir\"\\s*\"([^\"]+)\"");

        if (!nameMatch.Success || !installDirMatch.Success) return null;

        // معرف حتمي من اسم الملف (بدل Guid.NewGuid الذي يكسر المفضلة وساعات اللعب)
        // appmanifest_228980.acf → 228980
        var appId = idMatch.Success
            ? idMatch.Groups[1].Value
            : Path.GetFileNameWithoutExtension(manifestPath).Replace("appmanifest_", "");
        if (string.IsNullOrWhiteSpace(appId)) return null;

        var installPath = Path.Combine(steamAppsPath, "common", installDirMatch.Groups[1].Value);
        var name = nameMatch.Groups[1].Value;

        // حزم دعم وأدوات ستيم — نخفيها من المكتبة
        if (appId == "228980"                                          // Steamworks Common Redistributables
            || appId == "1070560"                                       // Steam Linux Runtime (scout)
            || name.Contains("Steam Linux Runtime", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Steamworks Common", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Redistributables", StringComparison.OrdinalIgnoreCase)
            || name.Contains("DirectX Runtime", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Proton ", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Depot", StringComparison.OrdinalIgnoreCase))
            return null;

        return new GameInfo
        {
            Id = appId,
            Name = name,
            InstallPath = installPath,
            ExecutablePath = $"steam://rungameid/{appId}",
            Platform = "Steam",
            CoverImageUrl = idMatch.Success
                ? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg"
                : null,
            IsInstalled = Directory.Exists(installPath)
        };
    }
}
