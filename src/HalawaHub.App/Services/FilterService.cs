using System.IO;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Models;

namespace HalawaHub.App.Services;

/// <summary>
/// تنفيذ IFilterService — منطق الفلترة والفرز.
/// </summary>
public class FilterService : IFilterService
{
    private const string NavAll = "الكل";
    private const string NavFavorite = "المفضلة";
    private const string NavInstalled = "مثبت";
    private const string NavRecent = "حديثًا";

    public IEnumerable<GameCardViewModel> ApplyFilter(
        IEnumerable<GameCardViewModel> games,
        string searchQuery,
        string selectedNavItem,
        string sortMode)
    {
        IEnumerable<GameCardViewModel> query = games;

        if (!string.IsNullOrWhiteSpace(searchQuery))
            query = query.Where(c => c.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

        switch (selectedNavItem)
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
                query = query.OrderByDescending(c => GetInstallTimestamp(c.Game)).Take(30);
                break;
            default:
                var platformName = selectedNavItem.Contains(" (")
                    ? selectedNavItem.Substring(0, selectedNavItem.IndexOf(" ("))
                    : selectedNavItem;
                query = query.Where(c => c.Platform == platformName);
                break;
        }

        query = sortMode switch
        {
            "lastplayed" => query.OrderByDescending(c => c.LastPlayed ?? DateTime.MinValue),
            "platform" => query.OrderBy(c => c.Platform).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
        };

        return query.ToList();
    }

    private static DateTime GetInstallTimestamp(GameInfo game)
    {
        try
        {
            if (Directory.Exists(game.InstallPath))
                return Directory.GetLastWriteTimeUtc(game.InstallPath);
        }
        catch { }
        return DateTime.MinValue;
    }
}
