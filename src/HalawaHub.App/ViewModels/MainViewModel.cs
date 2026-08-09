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
    private readonly PluginLoader _pluginLoader;
    private readonly List<IGameLibraryProvider> _builtInProviders;
    private readonly SteamGridDbClient _coverClient;

    public ObservableCollection<GameCardViewModel> Games { get; } = new();
    public ObservableCollection<GameCardViewModel> FilteredGames { get; } = new();
    public ObservableCollection<string> AvailablePlatforms { get; } = new() { "الكل" };

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

    private string _selectedPlatformFilter = "الكل";
    public string SelectedPlatformFilter
    {
        get => _selectedPlatformFilter;
        set
        {
            _selectedPlatformFilter = string.IsNullOrEmpty(value) ? "الكل" : value;
            OnPropertyChanged(nameof(SelectedPlatformFilter));
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
    private readonly UpdateChecker _updateChecker = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand LaunchGameCommand { get; }
    public RelayCommand OpenUpdateCommand { get; }

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

        _coverClient = new SteamGridDbClient(ConfigService.Load().SteamGridDbApiKey);

        RefreshCommand = new RelayCommand(_ => RefreshLibrary());
        LaunchGameCommand = new RelayCommand(param => LaunchGame((param as GameCardViewModel)?.Game));
        OpenUpdateCommand = new RelayCommand(_ => OpenUpdatePage(), _ => _updateDownloadUrl != null);

        RefreshLibrary();

        // فحص تحديث بالخلفية عند فتح البرنامج، بدون ما يعطّل فتح الواجهة
        _ = CheckForUpdateAsync();
    }

    private async Task CheckForUpdateAsync()
    {
        var update = await _updateChecker.CheckForUpdateAsync();
        if (update is not { IsNewer: true }) return;

        UpdateMessage = $"يتوفر إصدار جديد: v{update.LatestVersion} (لديك v{AppInfo.Version})";
        _updateDownloadUrl = update.DownloadUrl;
        OpenUpdateCommand.RaiseCanExecuteChanged();
    }

    private void OpenUpdatePage()
    {
        if (string.IsNullOrEmpty(_updateDownloadUrl)) return;

        try
        {
            Process.Start(new ProcessStartInfo(_updateDownloadUrl) { UseShellExecute = true });
        }
        catch
        {
            // تجاهل، ما يستاهل مقاطعة المستخدم بخطأ لمجرد فشل فتح رابط
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
        AvailablePlatforms.Add("الكل");
        foreach (var p in platforms) AvailablePlatforms.Add(p);

        if (!AvailablePlatforms.Contains(_selectedPlatformFilter))
            _selectedPlatformFilter = "الكل";

        OnPropertyChanged(nameof(SelectedPlatformFilter));
    }

    private void ApplyFilter()
    {
        FilteredGames.Clear();

        IEnumerable<GameCardViewModel> query = Games;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
            query = query.Where(c => c.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        if (SelectedPlatformFilter != "الكل")
            query = query.Where(c => c.Platform == SelectedPlatformFilter);

        foreach (var card in query)
            FilteredGames.Add(card);
    }

    private void LaunchGame(GameInfo? game)
    {
        if (game == null) return;

        StatusMessage = null;

        try
        {
            var psi = new ProcessStartInfo(game.ExecutablePath) { UseShellExecute = true };

            if (!string.IsNullOrEmpty(game.LaunchArguments))
                psi.Arguments = game.LaunchArguments;

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            StatusMessage = $"تعذر تشغيل {game.Name}: {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
