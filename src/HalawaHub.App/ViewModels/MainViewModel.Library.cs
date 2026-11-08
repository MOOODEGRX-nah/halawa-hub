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

    private readonly SingleFlightRunner _scanRunner = new();

    private bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            _isRefreshing = value;
            OnPropertyChanged(nameof(IsRefreshing));
        }
    }

    /// يفحص المكتبات على خيط خلفي (مزود Xbox قد يأخذ حتى 60 ثانية) ثم يملأ
    /// الواجهة على خيطها. طلب تحديث أثناء فحص جارٍ يُتجاهل، والقائمة الحالية
    /// تبقى ظاهرة حتى تصل النتائج.
    private async Task RefreshLibraryAsync()
    {
        if (_scanRunner.IsRunning) return;

        StatusMessage = null;
        IsRefreshing = true;

        var allProviders = _builtInProviders.Concat(_pluginLoader.LibraryProviders).ToList();
        var allTools = _pluginLoader.GameTools;

        try
        {
            var (ran, result) = await _scanRunner.RunAsync(() =>
            {
                // B06: إبطال كاش Xbox حتى تظهر الألعاب المثبتة حديثًا فورًا
                XboxLibraryProvider.InvalidateCache();
                return _libraryService.ScanAllLibraries(allProviders, allTools);
            });

            if (ran && result != null)
                ApplyScanResult(result, allTools);
        }
        catch (Exception ex)
        {
            Log.Error("فشل فحص المكتبات", ex);
            StatusMessage = $"تعذر فحص المكتبات: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplyScanResult(LibraryScanResult result, IEnumerable<IGameTool> allTools)
    {
        Games.Clear();

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

    private CancellationTokenSource? _coverLoadCts;
    private string? _coverLoadKey;

    private async Task LoadMissingCoversAsync()
    {
        // طلب جديد (تحديث أو مفتاح جديد) يلغي الجولة السابقة
        _coverLoadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _coverLoadCts = cts;

        var client = _coverClient;
        _coverLoadKey = SteamGridDbApiKey;

        var targets = Games.Where(c => !c.HasCoverImage).ToList();

        foreach (var card in targets)
        {
            if (cts.IsCancellationRequested) return;

            var url = await client.FindCoverUrlAsync(card.Name);
            if (!cts.IsCancellationRequested && !string.IsNullOrEmpty(url))
                card.SetCoverUrl(url);
        }
    }

    /// يحفظ أي تعديل معلق بالإعدادات ويطبق المفتاح الجديد فورًا
    public void FlushPendingSettings() => _apiKeySaveDebouncer.Flush();

    internal bool IsCoverClientConfigured => _coverClient.IsConfigured;

    private void ApplyApiKeyChange()
    {
        ConfigService.Save(_config);
        _coverClient = new SteamGridDbClient(_config.SteamGridDbApiKey);
    }

    private void CloseSettings()
    {
        IsSettingsOpen = false;
        FlushPendingSettings();

        // مفتاح جديد بدون ضغط "تحقق": نكمّل الأغلفة الناقصة به عند إغلاق الإعدادات
        if (_coverClient.IsConfigured && _coverLoadKey != SteamGridDbApiKey)
            _ = LoadMissingCoversAsync();
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
