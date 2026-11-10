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
        string navRecent,
        string? platform = null,
        bool includeHidden = false,
        bool reverse = false)
    {
        // الألعاب المخفية لا تظهر في أي تبويب ما لم يطلب المستخدم إظهارها
        IEnumerable<GameCardViewModel> query = includeHidden ? games : games.Where(c => !c.IsHidden);

        if (!string.IsNullOrEmpty(platform))
            query = query.Where(c => c.Platform == platform);

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

        // الاتجاه الطبيعي لكل ترتيب (الاسم أ←ي، والأحدث/الأكثر أولًا)، و"اعكس" يقلبه
        var byName = StringComparer.OrdinalIgnoreCase;
        query = (sortMode, reverse) switch
        {
            ("lastplayed", false) => query.OrderByDescending(c => c.LastPlayed ?? DateTime.MinValue).ThenBy(c => c.Name, byName),
            ("lastplayed", true) => query.OrderBy(c => c.LastPlayed ?? DateTime.MaxValue).ThenBy(c => c.Name, byName),
            ("platform", false) => query.OrderBy(c => c.Platform).ThenBy(c => c.Name, byName),
            ("platform", true) => query.OrderByDescending(c => c.Platform).ThenBy(c => c.Name, byName),
            ("playtime", false) => query.OrderByDescending(c => c.PlayTimeTotal).ThenBy(c => c.Name, byName),
            ("playtime", true) => query.OrderBy(c => c.PlayTimeTotal).ThenBy(c => c.Name, byName),
            ("added", false) => query.OrderByDescending(c => c.DateAdded).ThenBy(c => c.Name, byName),
            ("added", true) => query.OrderBy(c => c.DateAdded).ThenBy(c => c.Name, byName),
            (_, true) => query.OrderByDescending(c => c.Name, byName),
            _ => query.OrderBy(c => c.Name, byName)
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
