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

public enum ApiKeyCheckState { Unknown, Checking, Valid, Invalid }

public partial class MainViewModel : INotifyPropertyChanged
{
    // القيم الثابتة لعناصر الشريط الجانبي (بخلاف أسماء المنصات الديناميكية)
    public const string NavAll = "الكل";
    public const string NavFavorite = "المفضلة";
    public const string NavInstalled = "المثبتة";
    public const string NavRecent = "المضافة حديثًا";

    private readonly PluginLoader _pluginLoader;
    private readonly List<IGameLibraryProvider> _builtInProviders;
    private SteamGridDbClient _coverClient;
    private readonly Debouncer _apiKeySaveDebouncer;
    private readonly SteamNewsClient _newsClient = new();
    private readonly ILibraryService _libraryService;
    private readonly IFilterService _filterService;
    private readonly IUpdateService _updateService;

    public ObservableCollection<GameCardViewModel> Games { get; } = new();
    public ObservableCollection<GameCardViewModel> FilteredGames { get; } = new();

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value ?? "";
            OnPropertyChanged(nameof(SearchQuery));
            ApplyFilter();
            RefreshSearchState();
        }
    }

    private string _selectedNavItem = NavAll;
    public string SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            _selectedNavItem = string.IsNullOrEmpty(value) ? NavAll : value;
            OnPropertyChanged(nameof(SelectedNavItem));
            OnPropertyChanged(nameof(LibraryTabIndex));
            OnPropertyChanged(nameof(ShowFreeGamesStrip));
            ApplyFilter();
        }
    }

    private GameCardViewModel? _continuePlayingGame;
    public GameCardViewModel? ContinuePlayingGame
    {
        get => _continuePlayingGame;
        set
        {
            _continuePlayingGame = value;
            OnPropertyChanged(nameof(ContinuePlayingGame));
            OnPropertyChanged(nameof(HasContinuePlaying));
        }
    }

    public bool HasContinuePlaying => ContinuePlayingGame != null;

    public ObservableCollection<GameCardViewModel> RecentlyInstalled { get; } = new();
    public ObservableCollection<GameCardViewModel> FavoriteQuickLaunch { get; } = new();
    public ObservableCollection<GameCardViewModel> RecentlyPlayed { get; } = new();
    public ObservableCollection<NewsItem> NewsEntries { get; } = new();

    public bool HasNews => NewsEntries.Count > 0;
    public bool HasFavoriteQuickLaunch => FavoriteQuickLaunch.Count > 0;
    public bool HasRecentlyPlayed => RecentlyPlayed.Count > 0;
    public bool HasRecentlyInstalled => RecentlyInstalled.Count > 0;

    private GameCardViewModel? _selectedGame;
    public GameCardViewModel? SelectedGame
    {
        get => _selectedGame;
        set
        {
            _selectedGame = value;
            OnPropertyChanged(nameof(SelectedGame));
        }
    }

    private GameCardViewModel? _detailsGame;
    public GameCardViewModel? DetailsGame
    {
        get => _detailsGame;
        set
        {
            _detailsGame = value;
            OnPropertyChanged(nameof(DetailsGame));
            OnPropertyChanged(nameof(IsGameDialogVisible));
        }
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    private string? _updateMessage;
    public string? UpdateMessage
    {
        get => _updateMessage;
        set
        {
            _updateMessage = value;
            OnPropertyChanged(nameof(UpdateMessage));
            OnPropertyChanged(nameof(HasUpdateMessage));
        }
    }

    public bool HasUpdateMessage => !string.IsNullOrEmpty(UpdateMessage);

    private string? _updateDownloadUrl;
    private string? _updateSha256;
    private bool _updateIsReleasePage;


    private string _sortMode = "name";
    public string SortMode
    {
        get => _sortMode;
        set
        {
            if (_sortMode == value) return;
            _sortMode = value;
            _config.SortMode = value;
            ConfigService.Save(_config);
            OnPropertyChanged(nameof(SortMode));
            ApplyFilter();
        }
    }

    private bool _showPlayTimeOnCards = true;
    /// إظهار وقت اللعب وآخر تشغيل على البطاقات
    public bool ShowPlayTimeOnCards
    {
        get => _showPlayTimeOnCards;
        set
        {
            if (_showPlayTimeOnCards == value) return;
            _showPlayTimeOnCards = value;
            _config.ShowPlayTimeOnCards = value;
            ConfigService.Save(_config);
            foreach (var card in Games) card.ShowStats = value;
            OnPropertyChanged(nameof(ShowPlayTimeOnCards));
        }
    }

    private bool _sortReverse;
    /// عكس اتجاه الترتيب الحالي
    public bool SortReverse
    {
        get => _sortReverse;
        set
        {
            if (_sortReverse == value) return;
            _sortReverse = value;
            _config.SortReverse = value;
            ConfigService.Save(_config);
            OnPropertyChanged(nameof(SortReverse));
            ApplyFilter();
        }
    }
    private bool _isUpdating;

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set
        {
            _isSettingsOpen = value;
            OnPropertyChanged(nameof(IsSettingsOpen));
        }
    }

    private readonly AppConfig _config;

    private string _steamGridDbApiKey;
    public string SteamGridDbApiKey
    {
        get => _steamGridDbApiKey;
        set
        {
            var newKey = value ?? "";
            if (newKey == _steamGridDbApiKey) return;

            _steamGridDbApiKey = newKey;
            OnPropertyChanged(nameof(CanSearchCovers));
            _config.SteamGridDbApiKey = newKey;

            // الحفظ وإنشاء عميل الأغلفة بعد توقف الكتابة، لا عند كل حرف
            _apiKeySaveDebouncer.Schedule();
            OnPropertyChanged(nameof(SteamGridDbApiKey));

            // أي تعديل بالمفتاح يلغي نتيجة التحقق السابقة، لين يتحقق من جديد
            ApiKeyStatus = ApiKeyCheckState.Unknown;

            // زر "تحقق" يعتمد على وجود مفتاح (كان يبقى معطلًا في أول تشغيل)
            VerifyApiKeyCommand.RaiseCanExecuteChanged();
        }
    }

    private ApiKeyCheckState _apiKeyStatus = ApiKeyCheckState.Unknown;
    public ApiKeyCheckState ApiKeyStatus
    {
        get => _apiKeyStatus;
        set
        {
            _apiKeyStatus = value;
            OnPropertyChanged(nameof(ApiKeyStatus));
            OnPropertyChanged(nameof(ApiKeyStatusText));
            OnPropertyChanged(nameof(HasApiKeyStatus));
            OnPropertyChanged(nameof(ApiKeyBadge));
        }
    }

    public bool HasApiKeyStatus => ApiKeyStatus != ApiKeyCheckState.Unknown;

    public string ApiKeyStatusText => ApiKeyStatus switch
    {
        ApiKeyCheckState.Checking => Loc.Instance["verifying"],
        ApiKeyCheckState.Valid => Loc.Instance["keyOk"],
        ApiKeyCheckState.Invalid => Loc.Instance["keyBad"],
        _ => ""
    };

    private bool _launchOnStartup;
    public bool LaunchOnStartup
    {
        get => _launchOnStartup;
        set
        {
            _launchOnStartup = value;
            StartupService.SetEnabled(value);
            OnPropertyChanged(nameof(LaunchOnStartup));
        }
    }

    private bool _showInAppMenu;
    public bool ShowInAppMenu
    {
        get => _showInAppMenu;
        set
        {
            _showInAppMenu = value;
            AppMenuService.SetEnabled(value);
            OnPropertyChanged(nameof(ShowInAppMenu));
        }
    }

    public string AppVersion => $"v{AppInfo.Version}";
    public string WindowTitleText => $"Halawa-Hub {AppVersion}";

    public ObservableCollection<string> ChangelogEntries { get; } = new();

    private bool _isChangelogOpen;
    public bool IsChangelogOpen
    {
        get => _isChangelogOpen;
        set
        {
            _isChangelogOpen = value;
            OnPropertyChanged(nameof(IsChangelogOpen));
        }
    }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand LaunchGameCommand { get; }
    public RelayCommand InstallUpdateCommand { get; }
    public RelayCommand SelectNavCommand { get; }
    public RelayCommand OpenDetailsCommand { get; }
    public RelayCommand CloseDetailsCommand { get; }
    public RelayCommand DeleteGameCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand CloseSettingsCommand { get; }
    public RelayCommand CheckForUpdatesNowCommand { get; }
    public RelayCommand OpenUrlCommand { get; }
    public RelayCommand VerifyApiKeyCommand { get; }
    public RelayCommand CloseChangelogCommand { get; }
    public RelayCommand GoLibraryCommand { get; }
    public RelayCommand GoDeveloperCommand { get; }
    public RelayCommand SelectPlatformCommand { get; }
    public RelayCommand SelectGameDialogSectionCommand { get; }
    public RelayCommand SelectSettingsSectionCommand { get; }
    public RelayCommand SetLanguageCommand { get; }
    public RelayCommand OpenInstallLocationCommand { get; }
    public RelayCommand SearchCoversCommand { get; }
    public RelayCommand CloseCoverSearchCommand { get; }
    public RelayCommand PickCoverCandidateCommand { get; }
    public RelayCommand RestoreCoverCommand { get; }

    public MainViewModel(ILibraryService libraryService, IFilterService filterService, IUpdateService updateService)
        : this(libraryService, filterService, updateService, startBackgroundTasks: true)
    {
    }

    /// startBackgroundTasks=false للاختبارات: بدون اتصال بالشبكة ولا كتابة من مهام خلفية
    internal MainViewModel(ILibraryService libraryService, IFilterService filterService, IUpdateService updateService, bool startBackgroundTasks)
    {
        // النواة تبحث عن أي DLL داخل مجلد Plugins بجانب الملف التنفيذي
        _libraryService = libraryService;
        _filterService = filterService;
        _updateService = updateService;
        var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        _pluginLoader = new PluginLoader(pluginsDir);
        _pluginLoader.LoadPlugins();
        BuildPluginItems();
        LoadCollections();
        LoadSubscriptions();

        // منصات مدمجة بالنواة كبداية، وأي منصة إضافية تُضاف كـ Plugin لاحقًا
        _builtInProviders = new List<IGameLibraryProvider>
        {
            new SteamLibraryProvider(),
            new GogLibraryProvider(),
            new EpicLibraryProvider(),
            new RiotLibraryProvider(),
            new XboxLibraryProvider(),
            new HeroicLibraryProvider(),
            new LutrisLibraryProvider(),
            new UbisoftLibraryProvider(),
            new EaLibraryProvider(),
            new RockstarLibraryProvider(),
            new ManualGamesProvider()
        };

        _config = ConfigService.Load();

        // قبل إنشاء أي نص: اللغة المحفوظة، والملفات القديمة بدون الحقل تفتح بالعربية
        Loc.Instance.Language = _config.Language;
        _showHiddenGames = _config.ShowHiddenGames;
        _sortMode = string.IsNullOrEmpty(_config.SortMode) ? "name" : _config.SortMode;
        _sortReverse = _config.SortReverse;
        _showPlayTimeOnCards = _config.ShowPlayTimeOnCards;
        // تبويب المكتبة عند الفتح (إعداد عام)
        if (_config.StartTab > 0 && _config.StartTab < LibraryTabs.Length) _selectedNavItem = LibraryTabs[_config.StartTab];
        foreach (var search in _config.RecentSearches ?? new List<string>())
            RecentSearches.Add(search);

        _coverClient = new SteamGridDbClient(_config.SteamGridDbApiKey);
        _steamGridDbApiKey = _config.SteamGridDbApiKey;
        _apiKeySaveDebouncer = new Debouncer(TimeSpan.FromMilliseconds(800), ApplyApiKeyChange);
        _launchOnStartup = StartupService.IsEnabled();
        StartupService.RefreshPathIfEnabled();
        _showInAppMenu = AppMenuService.IsEnabled();
        AppMenuService.RefreshPathIfEnabled();

        RefreshCommand = new RelayCommand(_ => { _ = RefreshLibraryAsync(); });
        LaunchGameCommand = new RelayCommand(param => LaunchGame(param as GameCardViewModel));
        InstallUpdateCommand = new RelayCommand(_ => { _ = InstallUpdateAsync(); }, _ => _updateDownloadUrl != null && !_isUpdating);
        SelectNavCommand = new RelayCommand(param => SelectedNavItem = param as string ?? NavAll);
        OpenDetailsCommand = new RelayCommand(param => OpenDetails(param as GameCardViewModel));
        CloseDetailsCommand = new RelayCommand(_ => CloseDetails());
        DeleteGameCommand = new RelayCommand(_ => DeleteGame());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        CloseSettingsCommand = new RelayCommand(_ => CloseSettings());
        CheckForUpdatesNowCommand = new RelayCommand(_ => { _ = CheckForUpdateAsync(manualCheck: true); });
        OpenUrlCommand = new RelayCommand(OpenUrl);
        VerifyApiKeyCommand = new RelayCommand(_ => { _ = VerifyApiKeyAsync(); }, _ => !string.IsNullOrWhiteSpace(SteamGridDbApiKey));
        CloseChangelogCommand = new RelayCommand(_ => IsChangelogOpen = false);
        GoLibraryCommand = new RelayCommand(_ => TopNavIndex = 0);
        GoDeveloperCommand = new RelayCommand(_ => TopNavIndex = 1);
        SelectPlatformCommand = new RelayCommand(param => { if (param is PlatformChip chip) SelectedPlatform = chip.Key; });
        SelectGameDialogSectionCommand = new RelayCommand(param => { if (int.TryParse(param as string, out var i)) { ResetCoverSearch(); GameDialogSection = i; } });
        SelectSettingsSectionCommand = new RelayCommand(param => { if (int.TryParse(param as string, out var i)) SettingsSection = i; });
        SetLanguageCommand = new RelayCommand(param => { if (param is string language) Language = language; });
        OpenInstallLocationCommand = new RelayCommand(_ => OpenInstallLocation());
        SearchCoversCommand = new RelayCommand(_ => { _ = SearchCoversAsync(); });
        CloseCoverSearchCommand = new RelayCommand(_ => ResetCoverSearch());
        PickCoverCandidateCommand = new RelayCommand(param => { _ = PickCoverCandidateAsync(param as CoverCandidate); });
        RestoreCoverCommand = new RelayCommand(_ => DetailsGame?.RestoreOriginalCover());

        // الاختبارات بلا حالة Steam حقيقية ولا ذاكرة مكتبة مشتركة
        StatusProbe = startBackgroundTasks ? GameStatusProbe.System : null;
        LibraryCachePath = startBackgroundTasks ? LibraryCacheService.DefaultPath : null;
        SteamPathProvider = startBackgroundTasks ? SteamLibraryProvider.GetSteamPath : null;

        // لا ننتظر الفحص هنا: النافذة تُفتح فورًا والمكتبة تمتلئ بعد ذلك
        _ = RefreshLibraryAsync();

        // مهام الخلفية عند فتح البرنامج، بالترتيب الصح (فحص التحديث أول عشان
        // خبر "تحديث متوفر" يظهر صح بقائمة الأخبار)
        if (startBackgroundTasks)
        {
            _ = InitializeBackgroundTasksAsync();
            _ = RecoverActiveSessionAsync();
            AskAboutPendingPlugins();
            StartWizardIfFirstRun();
            _ = LoadFreeGamesAsync();
            _ = RefreshOwnedGamesAsync();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
