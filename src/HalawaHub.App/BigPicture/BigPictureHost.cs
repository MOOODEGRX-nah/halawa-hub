using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.BigPicture;

/// <summary>الشاشة الكبيرة فوق MainViewModel: نفس الألعاب والتشغيل والإشعارات والإعدادات.</summary>
public sealed class BigPictureHost : IBpHost
{
    private readonly MainViewModel _main;
    private readonly Action _exit;
    private readonly Action _closeApp;
    private List<BpGame>? _games;

    private readonly Action<string>? _reopenAt;

    public BigPictureHost(MainViewModel main, Action exit, Action closeApp, Action<string>? reopenAt = null)
    {
        _main = main;
        _exit = exit;
        _closeApp = closeApp;
        _reopenAt = reopenAt;
        main.Config.BigPicture ??= new BpSettings();
        Watch();
        WatchLibrary();
    }

    public BpSettings Settings => _main.Config.BigPicture;

    public IReadOnlyList<BpGame> Games => _games ??= _main.Games.Where(c => !c.IsHidden).Select(ToBp).ToList();

    /// تُعاد قراءة المكتبة بعد فحص جديد
    public void Invalidate() => _games = null;

    public event Action<bool>? DataChanged;
    private readonly HashSet<GameCardViewModel> _sizeWatched = new();
    private Avalonia.Threading.DispatcherTimer? _rescan;

    /// الحجم يُحسب في الخلفية: نعيد الرسم حين يصل
    private void WatchSize(GameCardViewModel c)
    {
        if (!_sizeWatched.Add(c)) return;
        c.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameCardViewModel.SizeOnDiskText)) DataChanged?.Invoke(false);
        };
    }

    /// فحص جديد يضيف الألعاب واحدة واحدة: نعيد البناء مرة بعد أن يهدأ
    private void WatchLibrary()
    {
        _main.Games.CollectionChanged += (_, _) =>
        {
            _rescan?.Stop();
            _rescan = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _rescan.Tick += (_, _) => { _rescan?.Stop(); _games = null; DataChanged?.Invoke(true); };
            _rescan.Start();
        };
    }

    internal BpGame ToBp(GameCardViewModel c) => new()
    {
        Key = c.Game.Platform + ":" + c.Game.Id,
        Name = c.Name,
        Platform = c.Game.Platform,
        PlatformShort = GameCardViewModel.ShortenPlatform(c.Game.Platform),
        Hours = c.PlayTimeTotal.TotalHours,
        LastPlayed = c.LastPlayed is { } lp ? (lp.Kind == DateTimeKind.Utc ? lp.ToLocalTime() : lp) : null,
        DateAdded = c.DateAdded,
        HasUpdate = c.HasUpdate,
        IsInstalled = c.IsInstalled,
        IsFavorite = c.IsFavorite,
        CoverUrl = c.CoverImageUrl,
        CoverFallbackUrl = c.CoverImageUrlFallback,
        SteamAppId = c.HasNews ? c.Game.Id : null,
        HeroUrl = c.HasNews ? SteamHero(c.Game.Id) : null,
        HeaderUrl = c.HasNews ? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{c.Game.Id}/header.jpg" : c.CoverImageUrlFallback,
        SizeLookup = () => { WatchSize(c); c.EnsureSizeRequested(); return c.SizeOnDiskText; },
        Source = c,
    };

    private static readonly Lazy<string?> SteamRoot = new(() =>
    {
        try { return HalawaHub.Core.Library.SteamLibraryProvider.GetSteamPath(); }
        catch { return null; }
    });

    /// <summary>
    /// صورة البطل لألعاب Steam: من ذاكرة Steam المحلية (librarycache) إن وُجدت، وإلا من خادم
    /// Steam نفسه (تُحفظ بعدها في ذاكرة الأغلفة). اللعبة بلا صورة بطل تعرض غلافها مموّهًا.
    /// </summary>
    internal static string SteamHero(string appId)
    {
        if (SteamRoot.Value is { } root)
        {
            var cache = System.IO.Path.Combine(root, "appcache", "librarycache");
            foreach (var path in new[] { System.IO.Path.Combine(cache, appId + "_library_hero.jpg"), System.IO.Path.Combine(cache, appId, "library_hero.jpg") })
                if (System.IO.File.Exists(path)) return new Uri(path).AbsoluteUri;
        }
        return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_hero.jpg";
    }

    private static GameCardViewModel Card(BpGame g) => (GameCardViewModel)g.Source!;

    public void SaveSettings() => _main.SaveBigPictureSettings();

    public void SetLanguage(string language) => _main.Language = language;

    public void ToggleFavorite(BpGame game)
    {
        var card = Card(game);
        card.IsFavorite = !card.IsFavorite;
        game.IsFavorite = card.IsFavorite;
    }

    public async Task<BpLaunchResult> LaunchAsync(BpGame game, CancellationToken token)
    {
        var card = Card(game);
        var result = _main.LaunchFromBigPicture(card);
        if (!result.Ok) return result;

        // ننتظر أن تظهر اللعبة تعمل (يكتشفها متتبع الجلسة)، بحد أقصى 20 ثانية
        for (var i = 0; i < 40 && !card.IsRunning; i++)
            await Task.Delay(500, token);
        return result;
    }

    public void OpenUpdateInLauncher(BpGame game) => _main.OpenInLauncherFromBigPicture(Card(game));
    public void OpenLauncher(BpGame game) => _main.OpenInLauncherFromBigPicture(Card(game));

    public void OpenGameSettings(BpGame game)
    {
        // إعدادات اللعبة (الغلاف، معاملات التشغيل...) في نافذة اللعبة العادية، ثم العودة للشاشة الكبيرة
        _exit();
        _main.OpenDetailsCommand.Execute(Card(game));
        if (_reopenAt == null || !_main.IsDetailsOpen) return;
        var key = game.Key;
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainViewModel.IsDetailsOpen) || _main.IsDetailsOpen) return;
            _main.PropertyChanged -= OnChanged;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _reopenAt(key));
        }
        _main.PropertyChanged += OnChanged;
    }

    public void HideGame(BpGame game)
    {
        Card(game).IsHidden = true;
        _games = null;
        DataChanged?.Invoke(true);
    }

    /// الخبر يُفتح داخل Steam (steam://openurl) كما في التصميم، وإن تعذّر ففي المتصفح
    public void OpenNews(BpGame game, string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("steam://openurl/" + url) { UseShellExecute = true })?.Dispose(); }
        catch
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
            catch (Exception ex) { HalawaHub.Core.Log.Error("could not open news", ex); }
        }
    }

    public async Task<IReadOnlyList<(string Title, string Date, string Url)>> LoadNewsAsync(BpGame game)
    {
        var card = Card(game);
        if (!card.HasNews) return Array.Empty<(string, string, string)>();
        await _main.LoadGameNewsAsync(card);
        return _main.GameNews.Take(2)
            .Select(n => (n.Title, n.Date.ToLocalTime().ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture), n.Url))
            .ToList();
    }

    // ===== الإشعارات: نفس إشعارات النافذة العادية بعنوان ونص وإجراء كما في التصميم =====

    public event Action<BpNotification>? NotificationArrived;
    private bool _watching;

    private void Watch()
    {
        if (_watching) return;
        _watching = true;
        _main.Notifications.CollectionChanged += (_, e) =>
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Add || e.NewStartingIndex != 0) return;
            if (e.NewItems?[0] is NotificationItem n) NotificationArrived?.Invoke(Map(n, 0, true));
        };
    }

    public IReadOnlyList<BpNotification> Notifications
    {
        get
        {
            Watch();
            var unread = _main.UnreadNotifications;
            return _main.Notifications.Select((n, i) => Map(n, i, i < unread)).ToList();
        }
    }

    private BpNotification Map(NotificationItem n, int index, bool unread)
    {
        var L = Loc.Instance;
        var game = n.Game is { } card ? Games.FirstOrDefault(g => ReferenceEquals(g.Source, card)) ?? ToBp(card) : null;
        var (kind, title, body, actionId, label) = n.Category switch
        {
            "update" when game != null => ("upd", L.Format("bpNtUpdT", game.Name), L["bpNtUpdB"], "update", L["bpUpdNow"]),
            "launch" when game != null => ("fail", L.Format("bpFailTitle", game.Name), n.Text, "retry", L["bpRetry"]),
            "launch" => ("fail", n.Text, "", (string?)null, (string?)null),
            "scan" => ("scan", L["bpNtScanT"], n.Text, "show", L["bpNtShow"]),
            "appupdate" => ("app", L["bpNtAppT"], L["bpNtAppB"], null, null),
            "free" => ("free", L["bpNtFreeT"], FreeBody(n.Text), n.CanOpen ? "open" : null, n.CanOpen ? L["bpOpen"] : null),
            _ => ("scan", n.Text, "", null, null),
        };
        return new BpNotification
        {
            Id = n.At.Ticks + ":" + index,
            Kind = kind, Title = title, Body = body,
            Time = RelativeTime(n.At, DateTime.Now),
            Unread = unread,
            ActionId = actionId, ActionLabel = label, Game = game,
            Action = actionId == "open" ? () => { _exit(); n.Open?.Invoke(); } : null,
            Source = n,
        };
    }

    /// نص المجانيات في النافذة العادية يقول "اضغط لفتح العروض"؛ هنا أسماء الألعاب وموعد انتهائها كما في التصميم
    private string FreeBody(string fallback)
    {
        var games = _main.FreeGames;
        if (games.Count == 0) return fallback;
        var sep = Loc.Instance.IsRtl ? "، " : ", ";
        var ends = games[0].EndsText;
        if (!Loc.Instance.IsRtl && ends.Length > 0) ends = char.ToLowerInvariant(ends[0]) + ends[1..];
        return string.Join(sep, games.Select(g => g.Title)) + sep + ends + ".";
    }

    /// "الآن"، "قبل دقيقتين"، "قبل ساعة"، "أمس" كما في التصميم
    internal static string RelativeTime(DateTime at, DateTime now)
    {
        var L = Loc.Instance;
        var span = now - at;
        if (span.TotalMinutes < 1) return L["bpNow"];
        if (span.TotalMinutes < 60) return BpPlural.MinutesAgo((int)span.TotalMinutes);
        if (now.Date == at.Date) return BpPlural.HoursAgo(Math.Max(1, (int)span.TotalHours));
        return now.Date.AddDays(-1) == at.Date ? L["bpYesterday"] : at.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    public void DismissNotification(BpNotification item) => _main.DismissNotificationCommand.Execute(item.Source);
    public void ClearNotifications() => _main.ClearNotificationsCommand.Execute(null);
    public void MarkNotificationsRead() => _main.MarkNotificationsReadFromBigPicture();

    public void ExitBigPicture() => _exit();
    public void CloseApp() => _closeApp();
}
