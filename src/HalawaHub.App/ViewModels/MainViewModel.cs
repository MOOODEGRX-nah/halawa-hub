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
    private readonly SteamGridDbClient _coverClient;
    private readonly SteamNewsClient _newsClient = new();

    public ObservableCollection<GameCardViewModel> Games { get; } = new();
    public ObservableCollection<GameCardViewModel> FilteredGames { get; } = new();
    public ObservableCollection<string> AvailablePlatforms { get; } = new();

    private string _searchQuery = "";
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value;
            OnPropertyChanged(nameof(SearchQuery));
            OnPropertyChanged(nameof(IsHomeView));
            ApplyFilter();
        }
    }

    private string _selectedNavItem = NavAll;
    public string SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            _selectedNavItem = string.IsNullOrEmpty(value) ? NavAll : value;
            OnPropertyChanged(nameof(IsHomeView));
            ApplyFilter();
        }
    }

    /// الصفحة الرئيسية (لوحة Bento) تظهر بس لما تكون على "الكل" وبدون بحث نشط —
    /// أي فلترة ثانية (منصة، مفضلة، بحث...) تنقلك للقائمة العادية
    public bool IsHomeView => SelectedNavItem == NavAll && string.IsNullOrWhiteSpace(SearchQuery);

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
            OnPropertyChanged(nameof(IsDetailsOpen));
        }
    }

    public bool IsDetailsOpen => DetailsGame != null;

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
            OnPropertyChanged(nameof(HasNotification));
        }
    }

    public bool HasUpdateMessage => !string.IsNullOrEmpty(UpdateMessage);

    private string? _updateDownloadUrl;
    private string? _updateSha256;

    private string _sortMode = "name";
    public string SortMode
    {
        get => _sortMode;
        set
        {
            _sortMode = value;
            OnPropertyChanged(nameof(SortMode));
            ApplyFilter();
        }
    }
    private bool _isUpdating;
    private readonly UpdateChecker _updateChecker = new();

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
            _steamGridDbApiKey = value ?? "";
            _config.SteamGridDbApiKey = _steamGridDbApiKey;
            ConfigService.Save(_config);
            OnPropertyChanged(nameof(SteamGridDbApiKey));

            // أي تعديل بالمفتاح يلغي نتيجة التحقق السابقة، لين يتحقق من جديد
            ApiKeyStatus = ApiKeyCheckState.Unknown;
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
        }
    }

    public bool HasApiKeyStatus => ApiKeyStatus != ApiKeyCheckState.Unknown;

    public string ApiKeyStatusText => ApiKeyStatus switch
    {
        ApiKeyCheckState.Checking => "جاري التحقق...",
        ApiKeyCheckState.Valid => "✅ المفتاح يعمل بشكل صحيح",
        ApiKeyCheckState.Invalid => "❌ المفتاح غير صحيح أو فيه مشكلة اتصال",
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

    /// يستخدمها جرس الإشعارات لعرض نقطة حمراء — حاليًا الإشعار الوحيد
    /// عندنا هو توفر تحديث، وممكن نوسّعها لاحقًا لأنواع تانية
    public bool HasNotification => HasUpdateMessage;

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

    public MainViewModel(ILibraryService libraryService, )
    {
        // النواة تبحث عن أي DLL داخل مجلد Plugins بجانب الملف التنفيذي
        var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
        _pluginLoader = new PluginLoader(pluginsDir);
        _pluginLoader.LoadPlugins();

        // منصات مدمجة بالنواة كبداية، وأي منصة إضافية تُضاف كـ Plugin لاحقًا
        _builtInProviders = new List<IGameLibraryProvider>
        {
            new SteamLibraryProvider(),
            new GogLibraryProvider(),
            new EpicLibraryProvider(),
            new RiotLibraryProvider(),
            new XboxLibraryProvider()
        };

        _config = ConfigService.Load();
        _coverClient = new SteamGridDbClient(_config.SteamGridDbApiKey);
        _steamGridDbApiKey = _config.SteamGridDbApiKey;
        _launchOnStartup = StartupService.IsEnabled();

        RefreshCommand = new RelayCommand(_ => RefreshLibrary());
        LaunchGameCommand = new RelayCommand(param => LaunchGame(param as GameCardViewModel));
        InstallUpdateCommand = new RelayCommand(_ => { _ = InstallUpdateAsync(); }, _ => _updateDownloadUrl != null && !_isUpdating);
        SelectNavCommand = new RelayCommand(param => SelectedNavItem = param as string ?? NavAll);
        OpenDetailsCommand = new RelayCommand(param => DetailsGame = param as GameCardViewModel);
        CloseDetailsCommand = new RelayCommand(_ => DetailsGame = null);
        DeleteGameCommand = new RelayCommand(_ => DeleteGame());
        OpenSettingsCommand = new RelayCommand(_ => IsSettingsOpen = true);
        CloseSettingsCommand = new RelayCommand(_ => IsSettingsOpen = false);
        CheckForUpdatesNowCommand = new RelayCommand(_ => { _ = CheckForUpdateAsync(manualCheck: true); });
        OpenUrlCommand = new RelayCommand(OpenUrl);
        VerifyApiKeyCommand = new RelayCommand(_ => { _ = VerifyApiKeyAsync(); }, _ => !string.IsNullOrWhiteSpace(SteamGridDbApiKey));
        CloseChangelogCommand = new RelayCommand(_ => IsChangelogOpen = false);

        RefreshLibrary();

        // مهام الخلفية عند فتح البرنامج، بالترتيب الصح (فحص التحديث أول عشان
        // خبر "تحديث متوفر" يظهر صح بقائمة الأخبار)
        _ = InitializeBackgroundTasksAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
