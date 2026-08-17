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
using HalawaHub.Core.Plugins;
using HalawaHub.Core.Updates;

namespace HalawaHub.App.ViewModels;

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
            ApplyFilter();
        }
    }

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
        }
    }

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

        RefreshLibrary();

        // فحص تحديث بالخلفية عند فتح البرنامج، بدون ما يعطّل فتح الواجهة
        _ = CheckForUpdateAsync();
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
                Games.Add(new GameCardViewModel(game, allTools));
            }
        }

        UpdateAvailablePlatforms();
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
