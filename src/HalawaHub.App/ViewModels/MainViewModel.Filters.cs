using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Covers;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.News;
using HalawaHub.Core.Plugins;
using HalawaHub.Core.Updates;

namespace HalawaHub.App.ViewModels;

public partial class MainViewModel
{
    private void ApplyFilter()
    {
        FilteredGames.Clear();

        IEnumerable<GameCardViewModel> query = Games;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
            query = query.Where(c => c.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        switch (SelectedNavItem)
        {
            case NavAll:
                break;
            case NavFavorite:
                query = query.Where(c => c.IsFavorite);
                break;
            case NavInstalled:
                query = query.Where(c => c.IsInstalled);
                break;
            case NavRecent:
                // ما عندنا سجل تشغيل بعد، فنقارب "حديثًا" بتاريخ آخر تعديل لمجلد
                // التثبيت نفسه — مو مثالي 100% لكنه مؤشر معقول لين نبني سجل حقيقي
                query = query.OrderByDescending(c => GetInstallTimestamp(c.Game)).Take(30);
                break;
            default:
                // استخراج اسم المنصة من label مثل "Steam (45)" → "Steam"
            var platformName = SelectedNavItem.Contains(" (")
                ? SelectedNavItem.Substring(0, SelectedNavItem.IndexOf(" ("))
                : SelectedNavItem;
            query = query.Where(c => c.Platform == platformName);
                break;
        }

        query = SortMode switch
        {
            "lastplayed" => query.OrderByDescending(c => c.LastPlayed ?? DateTime.MinValue),
            "platform" => query.OrderBy(c => c.Platform).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        };

        foreach (var card in query)
            FilteredGames.Add(card);
    }

    private static DateTime GetInstallTimestamp(GameInfo game)
    {
        try
        {
            if (Directory.Exists(game.InstallPath))
                return Directory.GetLastWriteTimeUtc(game.InstallPath);
        }
        catch
        {
            // مسار غير قابل للقراءة أو غير موجود
        }
        return DateTime.MinValue;
    }

}
