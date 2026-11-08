using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Covers;

namespace HalawaHub.App.ViewModels;

public sealed class PlatformChip : System.ComponentModel.INotifyPropertyChanged
{
    public PlatformChip(string? key, string label, int count)
    {
        Key = key;
        Label = label;
        Count = count;
    }

    public string? Key { get; }
    public string Label { get; }
    public int Count { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public sealed record SearchSuggestion(string Before, string Match, string After, string Text);
public enum UpdateCheckKind { Checking, UpToDate, Available, Failed }

/// حالة الواجهة الجديدة: الصفحات والتبويبات والمنصات والبحث والنوافذ
public partial class MainViewModel
{
    private static readonly string[] LibraryTabs = { NavAll, NavFavorite, NavInstalled, NavRecent };

    // ترتيب شريط المنصات في التصميم، ومنصات الإضافات تأتي بعدها
    private static readonly string[] PlatformOrder =
        { "Steam", "Epic Games", "GOG", "Riot Games", "Xbox / Microsoft Store", "Heroic", "Lutris" };

    private const int ChipWidth = 150;
    private const int MaxRecentSearches = 5;

    // ===== اللغة =====

    public string Language
    {
        get => Loc.Instance.Language;
        set
        {
            if (value == Loc.Instance.Language) return;
            Loc.Instance.Language = value;
            _config.Language = Loc.Instance.Language;
            ConfigService.Save(_config);
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(LanguageIndex));
            RefreshLanguageDependentState();
        }
    }

    /// مؤشر زر اللغة: عربي 0، English 1
    public int LanguageIndex => Language == Loc.English ? 1 : 0;

    public FlowDirection AppFlowDirection => Loc.Instance.FlowDirection;
    public FontFamily AppFontFamily => Loc.Instance.FontFamily;

    private void RefreshLanguageDependentState()
    {
        foreach (var card in Games) card.NotifyLanguageChanged();
        UpdatePlatformChips();
        RefreshSearchState();
        OnPropertyChanged(nameof(AppFlowDirection));
        OnPropertyChanged(nameof(AppFontFamily));
        OnPropertyChanged(nameof(UpdateStatusText));
        OnPropertyChanged(nameof(InstallButtonText));
        OnPropertyChanged(nameof(ApiKeyStatusText));
        OnPropertyChanged(nameof(GameDialogTitle));
        OnPropertyChanged(nameof(SettingsTitle));
    }

    // ===== الصفحة الرئيسية للبرنامج: المكتبة أو المطورين =====

    private int _topNavIndex;
    public int TopNavIndex
    {
        get => _topNavIndex;
        private set
        {
            if (_topNavIndex == value) return;
            _topNavIndex = value;
            OnPropertyChanged(nameof(TopNavIndex));
            OnPropertyChanged(nameof(IsLibraryPage));
            OnPropertyChanged(nameof(IsDeveloperPage));
        }
    }

    public bool IsLibraryPage => TopNavIndex == 0;
    public bool IsDeveloperPage => TopNavIndex == 1;

    // ===== تبويبات المكتبة =====

    public int LibraryTabIndex => Math.Max(0, Array.IndexOf(LibraryTabs, SelectedNavItem));

    public void SelectLibraryTab(int index)
    {
        if (index < 0 || index >= LibraryTabs.Length || index == LibraryTabIndex) return;
        CloseAllCardMenus();
        SelectedNavItem = LibraryTabs[index];
        OnPropertyChanged(nameof(LibraryTabIndex));
    }

    // ===== شريط المنصات =====

    public ObservableCollection<PlatformChip> PlatformChips { get; } = new();

    private string? _selectedPlatform;
    public string? SelectedPlatform
    {
        get => _selectedPlatform;
        set
        {
            if (_selectedPlatform == value) return;
            _selectedPlatform = value;
            foreach (var chip in PlatformChips) chip.IsSelected = chip.Key == value;
            OnPropertyChanged(nameof(SelectedPlatform));
            OnPropertyChanged(nameof(PlatformChipIndex));
            ApplyFilter();
        }
    }

    public int PlatformChipIndex
    {
        get
        {
            for (var i = 0; i < PlatformChips.Count; i++)
                if (PlatformChips[i].Key == _selectedPlatform) return i;
            return 0;
        }
    }

    public double PlatformBarWidth => Math.Max(1, PlatformChips.Count) * ChipWidth;

    private void UpdatePlatformChips()
    {
        var visible = Games.Where(c => ShowHiddenGames || !c.IsHidden).ToList();

        var platforms = visible
            .GroupBy(c => c.Platform)
            .OrderBy(g =>
            {
                var i = Array.IndexOf(PlatformOrder, g.Key);
                return i < 0 ? int.MaxValue : i;
            })
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        PlatformChips.Clear();
        PlatformChips.Add(new PlatformChip(null, Loc.Instance["all"], visible.Count));
        foreach (var group in platforms)
            PlatformChips.Add(new PlatformChip(group.Key, GameCardViewModel.ShortenPlatform(group.Key), group.Count()));

        if (_selectedPlatform != null && PlatformChips.All(c => c.Key != _selectedPlatform))
        {
            _selectedPlatform = null;
            OnPropertyChanged(nameof(SelectedPlatform));
        }

        foreach (var chip in PlatformChips) chip.IsSelected = chip.Key == _selectedPlatform;

        OnPropertyChanged(nameof(PlatformBarWidth));
        OnPropertyChanged(nameof(PlatformChipIndex));
    }

    // ===== الألعاب المخفية =====

    private bool _showHiddenGames;
    public bool ShowHiddenGames
    {
        get => _showHiddenGames;
        set
        {
            if (_showHiddenGames == value) return;
            _showHiddenGames = value;
            _config.ShowHiddenGames = value;
            ConfigService.Save(_config);
            OnPropertyChanged(nameof(ShowHiddenGames));
            UpdatePlatformChips();
            ApplyFilter();
        }
    }

    private bool _isPageEmpty;
    public bool IsPageEmpty
    {
        get => _isPageEmpty;
        private set
        {
            if (_isPageEmpty == value) return;
            _isPageEmpty = value;
            OnPropertyChanged(nameof(IsPageEmpty));
        }
    }

    // ===== الترتيب =====

    public void SetSortMode(string mode) => SortMode = mode;

    // ===== البحث =====

    public ObservableCollection<string> RecentSearches { get; } = new();
    public ObservableCollection<SearchSuggestion> SearchSuggestions { get; } = new();

    private bool _isSearchOpen;
    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        set
        {
            if (_isSearchOpen == value) return;
            _isSearchOpen = value;
            OnPropertyChanged(nameof(IsSearchOpen));
            RefreshSearchState();
        }
    }

    public bool ShowRecentSearches => IsSearchOpen && SearchQuery.Length == 0 && RecentSearches.Count > 0;
    public bool ShowSearchResults => IsSearchOpen && SearchSuggestions.Count > 0;
    public bool ShowSearchNoMatch => IsSearchOpen && HasSearchText && SearchSuggestions.Count == 0;
    public bool ShowSearchDropdown => ShowRecentSearches || ShowSearchResults || ShowSearchNoMatch;
    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchQuery);
    // عزل اتجاه النص (FSI/PDI): كلمة إنجليزية داخل جملة عربية كانت تبعثر علامتي « »
    public string SearchNoMatchText => Loc.Instance.Format("noResults", "⁨" + SearchQuery.Trim() + "⁩");

    private void RefreshSearchState()
    {
        SearchSuggestions.Clear();

        var query = SearchQuery.Trim();
        if (query.Length > 0)
        {
            var matches = Games
                .Where(c => ShowHiddenGames || !c.IsHidden)
                .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(4);

            foreach (var card in matches)
            {
                var at = card.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                SearchSuggestions.Add(new SearchSuggestion(
                    card.Name[..at], card.Name.Substring(at, query.Length), card.Name[(at + query.Length)..], card.Name));
            }
        }

        OnPropertyChanged(nameof(ShowRecentSearches));
        OnPropertyChanged(nameof(ShowSearchResults));
        OnPropertyChanged(nameof(ShowSearchNoMatch));
        OnPropertyChanged(nameof(ShowSearchDropdown));
        OnPropertyChanged(nameof(HasSearchText));
        OnPropertyChanged(nameof(SearchNoMatchText));
    }

    /// يحفظ البحث في "الأخيرة" عند الضغط على Enter أو اختيار اقتراح
    public void CommitSearch(string? text = null)
    {
        var value = (text ?? SearchQuery).Trim();
        if (text != null) SearchQuery = value;
        if (value.Length == 0) return;

        var existing = RecentSearches.FirstOrDefault(s => string.Equals(s, value, StringComparison.OrdinalIgnoreCase));
        if (existing != null) RecentSearches.Remove(existing);

        RecentSearches.Insert(0, value);
        while (RecentSearches.Count > MaxRecentSearches)
            RecentSearches.RemoveAt(RecentSearches.Count - 1);

        _config.RecentSearches = RecentSearches.ToList();
        ConfigService.Save(_config);
    }

    public void CloseSearch()
    {
        SearchQuery = "";
        IsSearchOpen = false;
    }

    // ===== قائمة البطاقة =====

    public void ToggleCardMenu(GameCardViewModel card)
    {
        var open = !card.IsMenuOpen;
        CloseAllCardMenus();
        card.IsMenuOpen = open;
    }

    public bool CloseAllCardMenus()
    {
        var closed = false;
        foreach (var card in Games.Where(c => c.IsMenuOpen))
        {
            card.IsMenuOpen = false;
            closed = true;
        }
        return closed;
    }

    public void ToggleFavoriteFromMenu(GameCardViewModel card)
    {
        card.IsFavorite = !card.IsFavorite;
        card.IsMenuOpen = false;
    }

    // ===== نافذة اللعبة =====

    private bool _isDetailsOpen;
    /// IsDetailsOpen يغلق فورًا، وDetailsGame يبقى حتى تنتهي حركة الإغلاق
    public bool IsDetailsOpen
    {
        get => _isDetailsOpen;
        private set
        {
            if (_isDetailsOpen == value) return;
            _isDetailsOpen = value;
            OnPropertyChanged(nameof(IsDetailsOpen));
        }
    }

    public bool IsGameDialogVisible => DetailsGame != null;

    private int _gameDialogSection;
    public int GameDialogSection
    {
        get => _gameDialogSection;
        private set
        {
            if (_gameDialogSection == value) return;
            _gameDialogSection = value;
            OnPropertyChanged(nameof(GameDialogSection));
            OnPropertyChanged(nameof(GameDialogTitle));
            if (value == 1) DetailsGame?.EnsureSizeRequested();
        }
    }

    public string GameDialogTitle => Loc.Instance[_gameDialogSection switch
    {
        1 => "files",
        2 => "custom",
        3 => "launch",
        4 => "tools",
        _ => "general"
    }];

    public int GameDialogSectionCount => DetailsGame?.HasTools == true ? 5 : 4;

    private void OpenDetails(GameCardViewModel? card)
    {
        if (card == null) return;
        CloseAllCardMenus();
        _gameDialogSection = -1;
        ResetCoverSearch();
        DetailsGame = card;
        GameDialogSection = 0;
        IsDetailsOpen = true;
        OnPropertyChanged(nameof(GameDialogSectionCount));
    }

    private async void CloseDetails()
    {
        if (!IsDetailsOpen) return;
        IsDetailsOpen = false;

        // اسم مخصص تغيّر أو إخفاء: نعيد الترتيب بعد إغلاق النافذة لا أثناء الكتابة
        ApplyFilter();

        if (MotionSettings.Enabled)
            await Task.Delay(230);

        if (!IsDetailsOpen)
            DetailsGame = null;
    }

    private void OpenInstallLocation()
    {
        var path = DetailsGame?.InstallPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatus(Loc.Instance.Format("openLocFailed", ex.Message));
        }
    }

    // ===== بحث SteamGridDB من نافذة اللعبة =====

    public ObservableCollection<CoverCandidate> CoverCandidates { get; } = new();

    public bool CanSearchCovers => !string.IsNullOrWhiteSpace(SteamGridDbApiKey);

    private bool _isCoverSearchOpen;
    public bool IsCoverSearchOpen
    {
        get => _isCoverSearchOpen;
        private set
        {
            _isCoverSearchOpen = value;
            OnPropertyChanged(nameof(IsCoverSearchOpen));
        }
    }

    private bool _isCoverSearching;
    public bool IsCoverSearching
    {
        get => _isCoverSearching;
        private set
        {
            _isCoverSearching = value;
            OnPropertyChanged(nameof(IsCoverSearching));
            OnPropertyChanged(nameof(HasNoCoverCandidates));
        }
    }

    public bool HasNoCoverCandidates => !IsCoverSearching && CoverCandidates.Count == 0;

    private void ResetCoverSearch()
    {
        IsCoverSearchOpen = false;
        CoverCandidates.Clear();
    }

    private async Task SearchCoversAsync()
    {
        var card = DetailsGame;
        if (card == null || !CanSearchCovers) return;

        FlushPendingSettings();
        CoverCandidates.Clear();
        IsCoverSearchOpen = true;
        IsCoverSearching = true;

        var candidates = await _coverClient.FindCoverCandidatesAsync(card.OriginalName);

        if (DetailsGame != card) return;

        CoverCandidates.Clear();
        foreach (var candidate in candidates) CoverCandidates.Add(candidate);
        IsCoverSearching = false;
    }

    private static readonly HttpClient CoverDownloader = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// ينزّل الغلاف المختار لمجلد البيانات، فيبقى بعد إعادة التشغيل مثل أي غلاف مخصص
    private async Task PickCoverCandidateAsync(CoverCandidate? candidate)
    {
        var card = DetailsGame;
        if (card == null || candidate == null) return;

        try
        {
            var bytes = await CoverDownloader.GetByteArrayAsync(candidate.Url);
            var path = BuildPickedCoverPath(card, candidate.Url);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes);

            card.SetCustomCover(path);
            IsCoverSearchOpen = false;
        }
        catch (Exception ex)
        {
            Log.Error("could not download the chosen cover", ex);
            ShowStatus(Loc.Instance["sgdbFailed"]);
        }
    }

    internal static string BuildPickedCoverPath(GameCardViewModel card, string url)
    {
        var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif")) ext = ".png";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(card.Platform + "|" + card.Game.Id + "|" + url)));
        return AppPaths.InData("custom-covers", hash[..24] + ext);
    }

    // ===== نافذة الإعدادات =====

    private bool _isSettingsVisible;
    /// IsSettingsOpen يغلق فورًا، وهذا يبقى حتى تنتهي حركة الإغلاق
    public bool IsSettingsVisible
    {
        get => _isSettingsVisible;
        private set
        {
            if (_isSettingsVisible == value) return;
            _isSettingsVisible = value;
            OnPropertyChanged(nameof(IsSettingsVisible));
        }
    }

    private int _settingsSection;
    public int SettingsSection
    {
        get => _settingsSection;
        private set
        {
            if (_settingsSection == value) return;
            _settingsSection = value;
            OnPropertyChanged(nameof(SettingsSection));
            OnPropertyChanged(nameof(SettingsTitle));
        }
    }

    public string SettingsTitle => Loc.Instance[_settingsSection switch
    {
        1 => "sLanguage",
        2 => "sApi",
        3 => "sAppearance",
        4 => "sStartup",
        5 => "sBuild",
        _ => "sUpdates"
    }];

    public bool IsStartupSupported => OperatingSystem.IsWindows();

    private void OpenSettings()
    {
        IsSettingsVisible = true;
        IsSettingsOpen = true;
    }

    // ===== معلومات النسخة =====

    public string DotNetVersion => Environment.Version.Major.ToString();
    public string AvaloniaVersion =>
        typeof(Avalonia.Application).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "";
    public string RepositoryName => $"{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}";
    public string RepositoryUrl => $"https://github.com/{RepositoryName}";
    public string SteamGridDbKeyUrl => "https://www.steamgriddb.com/profile/preferences";

    // ===== حالة التحديثات =====

    private UpdateCheckKind _updateKind = UpdateCheckKind.Checking;
    public UpdateCheckKind UpdateKind
    {
        get => _updateKind;
        private set
        {
            _updateKind = value;
            OnPropertyChanged(nameof(UpdateKind));
            OnPropertyChanged(nameof(UpdateBadge));
            OnPropertyChanged(nameof(UpdateStatusText));
            OnPropertyChanged(nameof(IsUpdateAvailable));
            OnPropertyChanged(nameof(IsCheckingForUpdates));
            OnPropertyChanged(nameof(InstallButtonText));
        }
    }

    public BadgeState UpdateBadge => _updateKind switch
    {
        UpdateCheckKind.Checking => BadgeState.Pending,
        UpdateCheckKind.Failed => BadgeState.Error,
        _ => BadgeState.Done
    };

    public bool IsUpdateAvailable => _updateKind == UpdateCheckKind.Available;
    public bool IsCheckingForUpdates => _updateKind == UpdateCheckKind.Checking;

    private string? _latestVersion;

    public string UpdateStatusText => _updateKind switch
    {
        UpdateCheckKind.Checking => Loc.Instance["checking"],
        UpdateCheckKind.Failed => Loc.Instance["updFail"],
        UpdateCheckKind.Available => Loc.Instance.Format("updAvailable", _latestVersion ?? "", AppInfo.Version),
        _ => Loc.Instance["latest"]
    };

    public string InstallButtonText => Loc.Instance[_updateIsReleasePage ? "openRelease" : "install"];

    // ===== حالة مفتاح API =====

    public BadgeState ApiKeyBadge => ApiKeyStatus switch
    {
        ApiKeyCheckState.Checking => BadgeState.Pending,
        ApiKeyCheckState.Valid => BadgeState.Done,
        ApiKeyCheckState.Invalid => BadgeState.Error,
        _ => BadgeState.None
    };

    // ===== رسالة الحالة (Toast) =====

    private int _statusToken;

    private async void ShowStatus(string message)
    {
        StatusMessage = message;

        var token = ++_statusToken;
        await Task.Delay(TimeSpan.FromSeconds(6));
        if (token == _statusToken) StatusMessage = null;
    }

    private void ClearStatus()
    {
        _statusToken++;
        StatusMessage = null;
    }
}
