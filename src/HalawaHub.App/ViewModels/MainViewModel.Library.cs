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
    private void RefreshLibrary()
    {
        StatusMessage = null;
        Games.Clear();

        var allProviders = _builtInProviders.Concat(_pluginLoader.LibraryProviders);
        var allTools = _pluginLoader.GameTools;
        var seen = new HashSet<string>();

        foreach (var provider in allProviders)
        {
            if (!provider.IsAvailable()) continue;

            List<HalawaHub.Core.Models.GameInfo> games;
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
                // حماية إضافية من أي تكرار، حتى لو جاء من مصدرين مختلفين بالخطأ
                if (!seen.Add($"{game.Platform}|{game.Id}")) continue;

                var card = new GameCardViewModel(game, allTools);
                card.FavoriteChanged += (_, _) => UpdateHomeViewCollections();
                Games.Add(card);
            }
        }

        UpdateAvailablePlatforms();
        UpdateHomeViewCollections();
        ApplyFilter();

        // لو فيه مفتاح SteamGridDB مُعد، نكمّل أغلفة المنصات اللي ما عندها مصدر مباشر
        if (_coverClient.IsConfigured)
            _ = LoadMissingCoversAsync();
    }

    private async Task LoadMissingCoversAsync()
    {
        // نسخة ثابتة من القائمة الحالية، عشان لو المستخدم ضغط "تحديث القائمة"
        // بالمنتصف ما نلعب بقائمة تغيّرت من تحتنا
        var targets = Games.Where(c => !c.HasCoverImage).ToList();

        foreach (var card in targets)
        {
            var url = await _coverClient.FindCoverUrlAsync(card.Name);
            if (!string.IsNullOrEmpty(url))
                card.SetCoverUrl(url);
        }
    }

    private void UpdateAvailablePlatforms()
    {
        // المنصات المدعومة ثابتة (حتى لو ما فيها ألعاب) — المستخدم يشوف كل الخيارات المتاحة
        var supportedPlatforms = new[] { "Steam", "Epic Games", "Riot Games", "Xbox / Microsoft Store", "GOG" };

        AvailablePlatforms.Clear();
        foreach (var platform in supportedPlatforms)
        {
            var count = Games.Count(c => c.Platform == platform);
            var label = count > 0 ? $"{platform} ({count})" : platform;
            AvailablePlatforms.Add(label);
        }

        // لو المنصة المختارة اختفت من القائمة (ما فيها ألعاب بعد التحديث)، نرجع لـ "الكل"
        var fixedItems = new[] { NavAll, NavFavorite, NavInstalled, NavRecent };
        if (!fixedItems.Contains(_selectedNavItem) && !AvailablePlatforms.Contains(_selectedNavItem))
            _selectedNavItem = NavAll;

        OnPropertyChanged(nameof(SelectedNavItem));
    }

    private bool _isListView;
    public bool IsListView
    {
        get => _isListView;
        set
        {
            _isListView = value;
            OnPropertyChanged(nameof(IsListView));
        }
    }

}
