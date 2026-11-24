using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture;

/// نشاط اللعبة كما يسجله Halawa: أيام متتالية وآخر جلسة
public sealed record BpActivity(int Streak, DateTime LastEnd, TimeSpan LastLength);

/// عمود في «لعبك هذا الأسبوع»
public sealed class BpWeekBar
{
    public required string Day { get; init; }
    public required double Px { get; init; }
    public bool IsToday { get; init; }
    public bool IsZero { get; init; }
}

/// <summary>
/// العمود الجانبي في الرئيسي (لقطات تتبدل كل 4.5 ثانية وهو ظاهر فقط، آخر خبر، نشاطك)،
/// و«لعبك هذا الأسبوع» في صفحة اللعبة.
/// </summary>
public sealed partial class BigPictureViewModel
{
    public static readonly TimeSpan ShotInterval = TimeSpan.FromSeconds(4.5);
    /// قاعدة الخمول: اللقطات تتبدل فقط خلال 30 ثانية من آخر ضغطة، ثم تثبت حتى الضغطة التالية
    public static readonly TimeSpan ShotIdleStop = TimeSpan.FromSeconds(30);
    internal DateTime LastInput { get; set; } = DateTime.UtcNow;
    /// للاختبارات
    internal Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;
    private bool RecentlyUsed => Now() - LastInput < ShotIdleStop;
    private const double WeekBarMax = 62;

    private IReadOnlyList<string> _shots = Array.Empty<string>();
    private int _shot;
    private bool _shotFlip;
    private Avalonia.Threading.DispatcherTimer? _shotTimer;
    private int _sideToken;
    private (string Title, string Date)? _latestNews;

    public ObservableCollection<bool> ShotDots { get; } = new();

    /// صورتان تتناوبان للتلاشي المتقاطع: الظاهرة تبقى والجديدة تظهر فوقها
    public string? ShotA { get; private set; }
    public string? ShotB { get; private set; }
    public bool ShowShotB => _shotFlip;
    public bool HasShots => _shots.Count > 0;
    /// بلا لقطات (غير Steam): الصورة العريضة للعبة أو غلافها
    public string? SideArtUrl => _hero?.ShowcaseUrl ?? _hero?.CoverUrl;
    public string ShotCaption => HasShots ? F("bpShotCap", _shot + 1, _shots.Count) : "";

    public bool HasLatestNews => _latestNews != null;
    public string LatestNewsTitle => _latestNews?.Title ?? "";
    public string LatestNewsDate => _latestNews?.Date ?? "";

    private BpActivity? Activity => _hero != null ? _host.Activity(_hero) : null;
    public bool HasActivity => Activity is { } a && (a.Streak > 0 || a.LastLength > TimeSpan.Zero);
    public string StreakText => Activity is { Streak: > 0 } a ? BpPlural.DaysInARow(a.Streak) : T("bpNoActivity");
    public string LastSessionText => Activity is { } a
        ? F("bpLastSession", BpPlural.Duration(a.LastLength), BpGame.RelativeDay(a.LastEnd, DateTime.Now))
        : "";

    /// اللعبة في الأعلى تغيّرت: اللقطات والخبر بعد توقف قصير (التمرير السريع لا يطلق طلبات لكل بطاقة)
    private void OnHeroChangedForSide()
    {
        var token = ++_sideToken;
        _shots = Array.Empty<string>();
        _shot = 0;
        _latestNews = null;
        SetShot(null);
        RebuildDots();
        if (_hero is { } g) _ = LoadSideAsync(g, token);
    }

    private async Task LoadSideAsync(BpGame g, int token)
    {
        await _delay(TimeSpan.FromMilliseconds(350));
        if (token != _sideToken) return;
        try
        {
            var shotsTask = _host.LoadScreenshotsAsync(g);
            var newsTask = g.SteamAppId != null ? _host.LoadNewsAsync(g) : Task.FromResult<IReadOnlyList<(string, string, string)>>(Array.Empty<(string, string, string)>());
            var shots = await shotsTask;
            var news = await newsTask;
            if (token != _sideToken) return;
            _shots = shots;
            _latestNews = news.Count > 0 ? (news[0].Item1, news[0].Item2) : null;
            _shot = 0;
            SetShot(_shots.Count > 0 ? _shots[0] : null);
            RebuildDots();
            Changed();
        }
        catch (Exception ex)
        {
            HalawaHub.Core.Log.Error("Big Picture side column load failed", ex);
        }
    }

    private void SetShot(string? url)
    {
        _shotFlip = !_shotFlip;
        if (_shotFlip) ShotB = url; else ShotA = url;
    }

    private void RebuildDots()
    {
        ShotDots.Clear();
        for (var i = 0; i < _shots.Count; i++) ShotDots.Add(i == _shot);
    }

    /// المؤقت يعمل فقط والرئيسي ظاهر بلا لوحة فوقه، والحركة مفعّلة، وللعبة أكثر من لقطة
    private void UpdateShotTimer()
    {
        var run = _screen == BpScreen.Home && _overlay == BpOverlay.None && MotionSettings.Enabled && _shots.Count > 1 && IsVisibleToUser && RecentlyUsed;
        if (run)
        {
            _shotTimer ??= new Avalonia.Threading.DispatcherTimer(ShotInterval, Avalonia.Threading.DispatcherPriority.Background, (_, _) => NextShot());
            if (!_shotTimer.IsEnabled) _shotTimer.Start();
        }
        else
        {
            _shotTimer?.Stop();
        }
    }

    internal bool IsShotTimerRunning => _shotTimer?.IsEnabled == true;

    /// النافذة ظاهرة للمستخدم (تضبطها النافذة)
    public bool IsVisibleToUser { get; set; } = true;

    internal void NextShot()
    {
        if (_shots.Count < 2) return;
        if (!RecentlyUsed) { _shotTimer?.Stop(); return; }
        _shot = (_shot + 1) % _shots.Count;
        SetShot(_shots[_shot]);
        for (var i = 0; i < ShotDots.Count; i++) ShotDots[i] = i == _shot;
        // صورة اللقطة فقط، لا كل الروابط
        foreach (var name in new[] { nameof(ShotA), nameof(ShotB), nameof(ShowShotB), nameof(ShotCaption) })
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    // ===== لعبك هذا الأسبوع (صفحة اللعبة) =====

    public IReadOnlyList<BpWeekBar> WeekBars
    {
        get
        {
            var hours = _hero != null ? _host.WeekHours(_hero) : new double[7];
            var max = Math.Max(hours.Max(), 0.0001);
            var today = DateTime.Now.Date;
            var culture = CultureInfo.GetCultureInfo(IsRtl ? "ar-EG" : "en-US");
            return hours.Select((h, i) => new BpWeekBar
            {
                Day = culture.DateTimeFormat.GetAbbreviatedDayName(today.AddDays(i - 6).DayOfWeek),
                Px = h <= 0 ? 4 : Math.Max(4, Math.Round(h / max * WeekBarMax)),
                IsToday = i == 6,
                IsZero = h <= 0,
            }).ToList();
        }
    }

    public string WeekTotalText
    {
        get
        {
            var total = _hero != null ? _host.WeekHours(_hero).Sum() : 0;
            var rounded = Math.Round(total, 1).ToString("0.#", CultureInfo.InvariantCulture);
            return IsRtl ? rounded + " ساعة" : rounded + " h";
        }
    }
}
