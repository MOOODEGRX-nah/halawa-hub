using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Covers;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.App.ViewModels;

public partial class MainViewModel
{

    private void RefreshLibrary()
    {
        StatusMessage = null;
        Games.Clear();

        var allProviders = _builtInProviders.Concat(_pluginLoader.LibraryProviders);
        var allTools = _pluginLoader.GameTools;

        // استخدام الـ service
        var result = _libraryService.ScanAllLibraries(allProviders, allTools);

        // بناء GameCardViewModels من النتائج
        foreach (var game in result.Games)
        {
            var card = new GameCardViewModel(game, allTools);
            card.FavoriteChanged += (_, _) => UpdateHomeViewCollections();
            Games.Add(card);
        }

        UpdateAvailablePlatforms(result.PlatformCounts);
        UpdateHomeViewCollections();
        ApplyFilter();

        // لو فيه مفتاح SteamGridDB مُعد، نكمّل أغلفة المنصات اللي ما عندها مصدر مباشر
        if (_coverClient.IsConfigured)
            _ = LoadMissingCoversAsync();
    }

    private async Task LoadMissingCoversAsync()
    {
        var targets = Games.Where(c => !c.HasCoverImage).ToList();

        foreach (var card in targets)
        {
            var url = await _coverClient.FindCoverUrlAsync(card.Name);
            if (!string.IsNullOrEmpty(url))
                card.SetCoverUrl(url);
        }
    }

    private void UpdateAvailablePlatforms(Dictionary<string, int> platformCounts)
    {
        AvailablePlatforms.Clear();
        foreach (var kvp in platformCounts)
        {
            var label = kvp.Value > 0 ? $"{kvp.Key} ({kvp.Value})" : kvp.Key;
            AvailablePlatforms.Add(label);
        }

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
