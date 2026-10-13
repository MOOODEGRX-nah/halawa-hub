using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// تنفيذ ILibraryService — منطق فحص المكتبات مع تحصين ضد:
/// - provider يرمي exception (E3): لا يكسر بقية المسح
/// - provider يرمي في منتصف الـ enumeration: نحتفظ بالنتائج الجزئية
/// - منصات إضافات: PlatformCounts تُشتق من البيانات نفسها
/// </summary>
public class LibraryService : ILibraryService
{
    public LibraryScanResult ScanAllLibraries(
        IEnumerable<IGameLibraryProvider> providers,
        IEnumerable<IGameTool> tools)
    {
        var result = new LibraryScanResult();
        var seen = new HashSet<string>();

        foreach (var provider in providers)
        {
            var collected = new List<GameInfo>();
            try
            {
                if (!provider.IsAvailable()) continue;

                foreach (var game in provider.ScanLibrary())
                {
                    if (game is not null) collected.Add(game);
                }
            }
            catch (Exception ex)
            {
                // provider رمى — نحتفظ بما جُمع قبل الاستثناء ونكمّل للمنصات التالية
                Log.Error($"فشل فحص مكتبة منصة {provider.PlatformName}", ex);
            }

            foreach (var game in collected)
            {
                if (string.IsNullOrEmpty(game.Platform) || string.IsNullOrEmpty(game.Id)) continue;
                if (!seen.Add($"{game.Platform}|{game.Id}")) continue;
                result.Games.Add(game);
            }
        }

        // PlatformCounts من البيانات نفسها (يدعم منصات الإضافات تلقائيًا)
        foreach (var group in result.Games.GroupBy(g => g.Platform))
        {
            result.PlatformCounts[group.Key] = group.Count();
        }

        return result;
    }
}
