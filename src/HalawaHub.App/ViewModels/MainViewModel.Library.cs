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
            IsPageEmpty = !value && FilteredGames.Count == 0;
        }
    }

    private bool _isRefreshDone;
    /// علامة ✓ قصيرة على زر التحديث بعد انتهاء الفحص
    public bool IsRefreshDone
    {
        get => _isRefreshDone;
        private set
        {
            _isRefreshDone = value;
            OnPropertyChanged(nameof(IsRefreshDone));
        }
    }

    /// يفحص المكتبات على خيط خلفي (مزود Xbox قد يأخذ حتى 60 ثانية) ثم يملأ
    /// الواجهة على خيطها. طلب تحديث أثناء فحص جارٍ يُتجاهل، والقائمة الحالية
    /// تبقى ظاهرة حتى تصل النتائج.
    private async Task RefreshLibraryAsync()
    {
        if (_scanRunner.IsRunning) return;

        ClearStatus();
        IsRefreshDone = false;
        ScanError = null;
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
            {
                ApplyScanResult(result, allTools);
                _ = FlashRefreshDoneAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error("فشل فحص المكتبات", ex);
            ScanError = Loc.Instance.Format("scanFailed", ex.Message);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task FlashRefreshDoneAsync()
    {
        IsRefreshDone = true;
        await Task.Delay(TimeSpan.FromSeconds(1.4));
        IsRefreshDone = false;
    }

    private void ApplyScanResult(LibraryScanResult result, IEnumerable<IGameTool> allTools)
    {
        Games.Clear();

        // ألعاب قرص غير موصول تبقى من آخر فحص بدل أن تختفي
        var games = LibraryCachePath != null && StatusProbe != null
            ? LibraryCacheService.MergeOffline(LibraryCachePath, result.Games, StatusProbe.IsDriveMissing)
            : result.Games;

        // بناء GameCardViewModels من النتائج
        foreach (var game in games)
        {
            var card = new GameCardViewModel(game, allTools) { ShowStats = _showPlayTimeOnCards };
            card.FavoriteChanged += (_, _) =>
            {
                UpdateHomeViewCollections();
                if (SelectedNavItem == NavFavorite) ApplyFilter();
            };
            card.HiddenChanged += (_, _) => UpdatePlatformChips();
            Games.Add(card);
        }

        UpdatePlatformChips();
        UpdateHomeViewCollections();
        ApplyFilter();
        RefreshSearchState();
        _ = RefreshStatusesAsync();
        FirstSeenService.SaveIfChanged();
        _ = RefreshSteamPlaytimeAsync();
        if (IsListLayout) RequestListSizes();

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

            var url = await client.FindCoverUrlAsync(card.OriginalName);
            if (!cts.IsCancellationRequested && !string.IsNullOrEmpty(url))
                card.SetCoverUrl(url);
        }
    }

    /// يحفظ أي تعديل معلق بالإعدادات ويطبق المفتاح الجديد فورًا
    public void FlushPendingSettings()
    {
        _apiKeySaveDebouncer.Flush();
        PersistActiveSession();
    }

    internal bool IsCoverClientConfigured => _coverClient.IsConfigured;

    private void ApplyApiKeyChange()
    {
        ConfigService.Save(_config);
        _coverClient = new SteamGridDbClient(_config.SteamGridDbApiKey);
        OnPropertyChanged(nameof(SecretStorageText));
    }

    /// أين يُحفظ مفتاح API على هذا الجهاز (يظهر تحت حقل المفتاح)
    public string SecretStorageText => Loc.Instance[
        (HalawaHub.Core.Security.SecretStore.BackendOf(_config.SteamGridDbApiKeyRef) ?? HalawaHub.Core.Security.SecretStore.PreferredBackend) switch
        {
            HalawaHub.Core.Security.SecretBackend.WindowsDpapi => "secretDpapi",
            HalawaHub.Core.Security.SecretBackend.SecretService => "secretKeyring",
            _ => "secretFile"
        }];

    private async void CloseSettings()
    {
        if (!IsSettingsOpen) return;
        IsSettingsOpen = false;
        FlushPendingSettings();

        // مفتاح جديد بدون ضغط "تحقق": نكمّل الأغلفة الناقصة به عند إغلاق الإعدادات
        if (_coverClient.IsConfigured && _coverLoadKey != SteamGridDbApiKey)
            _ = LoadMissingCoversAsync();

        if (MotionSettings.Enabled)
            await Task.Delay(230);

        if (!IsSettingsOpen)
            IsSettingsVisible = false;
    }

}
