using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Library;

namespace HalawaHub.App.ViewModels;

/// سطر منصة في الإعدادات > المنصات
public sealed class PlatformRow
{
    public required string Name { get; init; }
    public required bool Detected { get; init; }
    public required string PathText { get; init; }
    public required int GameCount { get; init; }
    public required PlatformCapabilities Caps { get; init; }

    /// مشغّل حقيقي (لا قائمة "أضفتها بنفسك")؛ معالج أول تشغيل يعرض المشغّلات فقط
    public bool IsLauncher { get; init; } = true;

    public string DetectedText => Loc.Instance[Detected ? "platDetected" : "platNotFound"];
    public string GamesText => Loc.Instance.Format("platGames", GameCount);
    public string CapsText
    {
        get
        {
            var l = Loc.Instance;
            string Mark(bool ok, string key) => (ok ? "✓ " : "✗ ") + l[key];
            var updates = Caps.Updates ? "✓ " + l["capUpdates"] : Caps.RepairOnly ? "◐ " + l["capRepairOnly"] : "✗ " + l["capUpdates"];
            return string.Join("   ", Mark(Caps.InstalledGames, "capGames"), Mark(Caps.PlayTime, "capPlayTime"), updates,
                Mark(Caps.RunningOutside, "capRunning"));
        }
    }
}

public partial class MainViewModel
{
    public ObservableCollection<PlatformRow> PlatformRows { get; } = new();

    private bool _isDetectingPlatforms;
    public bool IsDetectingPlatforms
    {
        get => _isDetectingPlatforms;
        private set { if (_isDetectingPlatforms == value) return; _isDetectingPlatforms = value; OnPropertyChanged(nameof(IsDetectingPlatforms)); }
    }

    private RelayCommand? _redetectCommand;
    public RelayCommand RedetectPlatformsCommand => _redetectCommand ??= new RelayCommand(_ => RefreshPlatformRows());

    /// IsAvailable رخيص لكل المنصات، لكن يُشغَّل على خيط خلفي احتياطًا
    internal async void RefreshPlatformRows()
    {
        if (IsDetectingPlatforms) return;
        IsDetectingPlatforms = true;
        try
        {
            var providers = _builtInProviders.Concat(_pluginLoader.LibraryProviders).ToList();
            var counts = Games.GroupBy(c => c.Game.Platform).ToDictionary(g => g.Key, g => g.Count());

            var rows = await Task.Run(() => providers.Select(p =>
            {
                bool detected;
                try { detected = p.IsAvailable(); }
                catch (Exception ex) { Log.Error($"{p.PlatformName}: detection failed", ex); detected = false; }
                // المنصات الخاصة بنظام آخر لا تُعرض (Epic/GOG/Riot/Xbox على لينكس، Heroic/Lutris على ويندوز)
                var relevant = detected || p.PlatformName == ManualGames.Platform
                    || (OperatingSystem.IsWindows() ? p.PlatformName is not ("Heroic" or "Lutris")
                                                    : p.PlatformName is "Steam" or "Heroic" or "Lutris");
                if (!relevant) return null;
                string? path = null;
                try { path = detected ? PlatformCatalog.DetectedPath(p.PlatformName) : null; } catch { /* عرض فقط */ }
                return new PlatformRow
                {
                    Name = GameCardViewModel.ShortenPlatform(p.PlatformName),
                    IsLauncher = p.PlatformName != ManualGames.Platform,
                    Detected = detected || (p.PlatformName == ManualGames.Platform),
                    PathText = path ?? "—",
                    GameCount = counts.TryGetValue(p.PlatformName, out var n) ? n : 0,
                    Caps = PlatformCatalog.For(p.PlatformName)
                };
            }).Where(r => r != null).ToList());

            PlatformRows.Clear();
            foreach (var row in rows) PlatformRows.Add(row!);
        }
        catch (Exception ex)
        {
            Log.Error("platform detection failed", ex);
        }
        finally
        {
            IsDetectingPlatforms = false;
        }
    }
}
