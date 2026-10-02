using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Models;

namespace HalawaHub.App.Services;

/// <summary>
/// تنفيذ IFilterService — منطق الفلترة والفرز.
/// </summary>
public class FilterService : IFilterService
{
    public IEnumerable<GameCardViewModel> ApplyFilter(
        IEnumerable<GameCardViewModel> games,
        string searchQuery,
        string selectedNavItem,
        string sortMode,
        string navAll,
        string navFavorite,
        string navInstalled,
        string navRecent)
    {
        IEnumerable<GameCardViewModel> query = games;

        if (!string.IsNullOrWhiteSpace(searchQuery))
            query = query.Where(c => c.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));

        if (selectedNavItem == navAll)
        {
            // لا فلتر إضافي
        }
        else if (selectedNavItem == navFavorite)
        {
            query = query.Where(c => c.IsFavorite);
        }
        else if (selectedNavItem == navInstalled)
        {
            query = query.Where(c => c.IsInstalled);
        }
        else if (selectedNavItem == navRecent)
        {
            query = query.OrderByDescending(c => GetInstallTimestamp(c.Game)).Take(30);
        }
        else
        {
            // استخراج اسم المنصة من label مثل "Steam (45)" → "Steam"
            var platformName = selectedNavItem.Contains(" (")
                ? selectedNavItem.Substring(0, selectedNavItem.IndexOf(" ("))
                : selectedNavItem;
            query = query.Where(c => c.Platform == platformName);
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
