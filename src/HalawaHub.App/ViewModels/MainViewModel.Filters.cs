using System;
using System.IO;
using System.Linq;
using HalawaHub.App.Services;
using HalawaHub.Core.Models;

namespace HalawaHub.App.ViewModels;

public partial class MainViewModel
{
    /// <summary>
    /// يطبّق الفلتر عبر FilterService ويعبّي FilteredGames.
    /// ثوابت التنقل تمرر من هنا — مصدر الحقيقة الوحيد.
    /// </summary>
    private void ApplyFilter()
    {
        FilteredGames.Clear();
        var filtered = _filterService.ApplyFilter(
            Games, SearchQuery, SelectedNavItem, SortMode,
            NavAll, NavFavorite, NavInstalled, NavRecent,
            platform: SelectedPlatform, includeHidden: ShowHiddenGames, reverse: SortReverse);
        var inCollection = ActiveCollectionFilter();
        foreach (var card in MergeCards(filtered.Where(inCollection)))
            FilteredGames.Add(card);

        IsPageEmpty = !IsRefreshing && FilteredGames.Count == 0;
    }

    // مستدعاة من UpdateHomeViewCollections بـ Updates.cs — لا تحذفها أبدًا (حادث 0.0.10.41)
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
