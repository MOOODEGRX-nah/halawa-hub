using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// تنفيذ ILibraryService — منطق فحص المكتبات.
/// </summary>
public class LibraryService : ILibraryService
{
    public LibraryScanResult ScanAllLibraries(
        IEnumerable<IGameLibraryProvider> providers,
        IEnumerable<IGameTool> tools)
    {
        var result = new LibraryScanResult();
        var seen = new HashSet<string>();
        var toolsList = tools.ToList();

        foreach (var provider in providers)
        {
            if (!provider.IsAvailable()) continue;

            List<GameInfo> games;
            try
            {
                games = provider.ScanLibrary().ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"فشل فحص مكتبة منصة {provider.PlatformName}", ex);
                continue;
            }

            foreach (var game in games)
            {
                // حماية من التكرار
                if (!seen.Add($"{game.Platform}|{game.Id}")) continue;
                result.Games.Add(game);
            }
        }

        // حساب أعداد المنصات
        var supportedPlatforms = new[] { "Steam", "Epic Games", "Riot Games", "Xbox / Microsoft Store", "GOG" };
        foreach (var platform in supportedPlatforms)
        {
            var count = result.Games.Count(g => g.Platform == platform);
            result.PlatformCounts[platform] = count;
        }

        return result;
    }
}
