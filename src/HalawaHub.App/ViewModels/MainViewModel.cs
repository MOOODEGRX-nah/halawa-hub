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

public class MainViewModel : INotifyPropertyChanged
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
            OnPropertyChanged(nameof(SelectedNavItem));
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

    public MainViewModel()
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

    private async Task InitializeBackgroundTasksAsync()
    {
        await CheckForUpdateAsync();
        _ = LoadNewsAsync();
        _ = CheckChangelogAsync();
    }

    private async Task LoadNewsAsync()
    {
        NewsEntries.Clear();

        // خبر تحديث البرنامج نفسه، لو متوفر فعليًا
        if (HasUpdateMessage)
            NewsEntries.Add(new NewsItem("Halawa-Hub", UpdateMessage ?? "", "", DateTime.UtcNow));

        // آخر خبر لأول لعبتين Steam بمكتبتك (API عام مجاني، بدون مفتاح)
        var steamGames = Games.Where(c => c.Platform == "Steam" && !string.IsNullOrEmpty(c.Game.Id))
                               .Take(2).ToList();

        foreach (var g in steamGames)
        {
            var items = await _newsClient.GetNewsForAppAsync(g.Name, g.Game.Id, count: 1);
            foreach (var item in items) NewsEntries.Add(item);
        }

        OnPropertyChanged(nameof(HasNews));
    }

    private void UpdateHomeViewCollections()
    {
        // استمر من حيث توقفت — آخر لعبة شغّلتها فعليًا (فاضي لين تشغّل أول لعبة)
        ContinuePlayingGame = Games.Where(c => c.LastPlayed != null)
                                    .OrderByDescending(c => c.LastPlayed)
                                    .FirstOrDefault();

        // تم تحميله حديثًا — أحدث 3 حسب تاريخ تعديل مجلد التثبيت
        RecentlyInstalled.Clear();
        foreach (var c in Games.OrderByDescending(c => GetInstallTimestamp(c.Game)).Take(3))
            RecentlyInstalled.Add(c);
        OnPropertyChanged(nameof(HasRecentlyInstalled));

        // تشغيل سريع — المفضلة (لحد 4)
        FavoriteQuickLaunch.Clear();
        foreach (var c in Games.Where(c => c.IsFavorite).Take(4))
            FavoriteQuickLaunch.Add(c);
        OnPropertyChanged(nameof(HasFavoriteQuickLaunch));

        // آخر الألعاب اللي لعبتها (لحد 5)
        RecentlyPlayed.Clear();
        foreach (var c in Games.Where(c => c.LastPlayed != null).OrderByDescending(c => c.LastPlayed).Take(5))
            RecentlyPlayed.Add(c);
        OnPropertyChanged(nameof(HasRecentlyPlayed));
    }

    private async Task CheckChangelogAsync()
    {
        if (_config.LastSeenVersion == AppInfo.Version) return;

        var changes = await ChangelogService.GetChangesForVersionAsync(AppInfo.GitHubOwner, AppInfo.GitHubRepo, AppInfo.Version);

        _config.LastSeenVersion = AppInfo.Version;
        ConfigService.Save(_config);

        // ما فيه ملاحظات مسجّلة لهذا الإصدار (أو أول تشغيل بالأساس) — لا نزعج المستخدم
        if (changes.Count == 0) return;

        ChangelogEntries.Clear();
        foreach (var change in changes) ChangelogEntries.Add(change);
        IsChangelogOpen = true;
    }

    private void OpenUrl(object? param)
    {
        if (param is not string url || string.IsNullOrEmpty(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // تجاهل، ما يستاهل مقاطعة المستخدم بخطأ لمجرد فشل فتح رابط
        }
    }

    private async Task VerifyApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamGridDbApiKey))
        {
            ApiKeyStatus = ApiKeyCheckState.Invalid;
            return;
        }

        ApiKeyStatus = ApiKeyCheckState.Checking;

        var client = new SteamGridDbClient(SteamGridDbApiKey);
        var valid = await client.ValidateApiKeyAsync();

        ApiKeyStatus = valid ? ApiKeyCheckState.Valid : ApiKeyCheckState.Invalid;
    }

    private async Task CheckForUpdateAsync(bool manualCheck = false)
    {
        var update = await _updateChecker.CheckForUpdateAsync();

        if (update is not { IsNewer: true })
        {
            if (manualCheck) StatusMessage = "البرنامج محدّث لآخر إصدار.";
            return;
        }

        UpdateMessage = $"يتوفر إصدار جديد: v{update.LatestVersion} (لديك v{AppInfo.Version})";
        _updateDownloadUrl = update.DownloadUrl;
        InstallUpdateCommand.RaiseCanExecuteChanged();
    }

    private async Task InstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl) || _isUpdating) return;

        _isUpdating = true;
        InstallUpdateCommand.RaiseCanExecuteChanged();
        UpdateMessage = "جاري تحميل التحديث...";

        var success = await SelfUpdater.DownloadAndApplyAsync(_updateDownloadUrl, status => UpdateMessage = status);

        if (success)
        {
            UpdateMessage = "التحديث جاهز، البرنامج بيعيد التشغيل الآن...";
            await Task.Delay(1200);
            Environment.Exit(0);
        }
        else
        {
            UpdateMessage = "فشل التحديث التلقائي. جرّب لاحقًا أو حمّل من صفحة الإصدارات على GitHub يدويًا.";
            _isUpdating = false;
            InstallUpdateCommand.RaiseCanExecuteChanged();
        }
    }

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

            foreach (var game in provider.ScanLibrary())
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
        var platforms = Games.Select(c => c.Platform).Distinct().OrderBy(p => p).ToList();

        AvailablePlatforms.Clear();
        foreach (var p in platforms) AvailablePlatforms.Add(p);

        // لو المنصة المختارة اختفت من القائمة (ما فيها ألعاب بعد التحديث)، نرجع لـ "الكل"
        var fixedItems = new[] { NavAll, NavFavorite, NavInstalled, NavRecent };
        if (!fixedItems.Contains(_selectedNavItem) && !AvailablePlatforms.Contains(_selectedNavItem))
            _selectedNavItem = NavAll;

        OnPropertyChanged(nameof(SelectedNavItem));
    }

    private void ApplyFilter()
    {
        FilteredGames.Clear();

        IEnumerable<GameCardViewModel> query = Games;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
            query = query.Where(c => c.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        switch (SelectedNavItem)
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
                // ما عندنا سجل تشغيل بعد، فنقارب "حديثًا" بتاريخ آخر تعديل لمجلد
                // التثبيت نفسه — مو مثالي 100% لكنه مؤشر معقول لين نبني سجل حقيقي
                query = query.OrderByDescending(c => GetInstallTimestamp(c.Game)).Take(30);
                break;
            default:
                query = query.Where(c => c.Platform == SelectedNavItem);
                break;
        }

        foreach (var card in query)
            FilteredGames.Add(card);
    }

    private static DateTime GetInstallTimestamp(GameInfo game)
    {
        try
        {
            if (Directory.Exists(game.InstallPath))
                return Directory.GetLastWriteTimeUtc(game.InstallPath);
        }
        catch
        {
            // مسار غير قابل للقراءة أو غير موجود
        }
        return DateTime.MinValue;
    }

    private void LaunchGame(GameCardViewModel? card)
    {
        if (card == null) return;

        var game = card.Game;
        StatusMessage = null;

        try
        {
            var psi = new ProcessStartInfo(game.ExecutablePath) { UseShellExecute = true };

            var args = game.LaunchArguments ?? "";
            if (!string.IsNullOrWhiteSpace(card.LaunchParameters))
                args = string.IsNullOrEmpty(args) ? card.LaunchParameters : $"{args} {card.LaunchParameters}";

            if (!string.IsNullOrEmpty(args))
                psi.Arguments = args;

            Process.Start(psi);

            card.NotifyPlayed();
            UpdateHomeViewCollections();
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر تشغيل {game.Name}: {ex.Message}";
        }
    }

    private void DeleteGame()
    {
        if (DetailsGame == null) return;
        var game = DetailsGame.Game;

        // نعتمد فقط على أدوات الحذف الرسمية للمنصة نفسها (تفتح تأكيدها الخاص) —
        // ما نحذف أي ملفات مباشرة من هنا تفاديًا لأي خطر على بيانات المستخدم
        if (game.Platform == "Steam" && !string.IsNullOrEmpty(game.Id))
        {
            try
            {
                Process.Start(new ProcessStartInfo($"steam://uninstall/{game.Id}") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusMessage = $"تعذر فتح نافذة الحذف: {ex.Message}";
            }
        }
        else
        {
            StatusMessage = "الحذف المباشر مو مدعوم بعد لهذي المنصة — احذفها من اللانشر الرسمي.";
        }

        DetailsGame = null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
