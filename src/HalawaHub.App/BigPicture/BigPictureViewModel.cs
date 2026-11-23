using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// حالة الشاشة الكبيرة ومنطق التنقل، منقولة من دوال nav / act / back في
/// design-source/BigPicture.dc.html. لا تعرف شيئًا عن عناصر Avalonia: كل عنصر
/// قابل للتركيز له FocusKey، والواجهة ترسم حلقة التركيز حول العنصر ذي المفتاح الحالي.
/// dx منطقي (1 = نحو نهاية السطر)؛ Press يقلبه في العربية كما يفعل التصميم.
/// </summary>
public sealed partial class BigPictureViewModel : INotifyPropertyChanged
{
    public static readonly BpScreen[] Tabs = { BpScreen.Home, BpScreen.Library, BpScreen.Search };

    private readonly IBpHost _host;
    private readonly Func<TimeSpan, Task> _delay;
    private BpScreen _screen = BpScreen.Home;
    private BpOverlay _overlay = BpOverlay.None;
    private BpScreen _from = BpScreen.Home;
    private bool _navForward = true;

    public BigPictureViewModel(IBpHost host, Func<TimeSpan, Task>? delay = null)
    {
        _host = host;
        _delay = delay ?? Task.Delay;
        BuildHome();
        BuildLibrary();
        BuildSearch();
        BuildSettings();
        BuildGuide();
        _host.NotificationArrived += OnNotificationArrived;
        _host.DataChanged += library => { if (library) Reload(); else Changed(); };
        RefreshOverlayLabels();
        SyncFocus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    /// دفعت نحو حافة لا عنصر بعدها (dx, dy منطقيان)
    public event Action<int, int>? Bumped;
    /// ضغطة A (أو ما يقوم مقامها): وميض الحلقة وموجة النقاط
    public event Action? Activated;
    /// رسالة قصيرة تظهر ثلاث ثوانٍ
    public event Action<string>? Toast;

    public IBpHost Host => _host;
    public BpSettings Settings => _host.Settings;
    public bool IsRtl => Loc.Instance.IsRtl;

    public BpScreen Screen => _screen;
    public BpOverlay Overlay => _overlay;
    /// التبويب الظاهر في الشريط العلوي (صفحة اللعبة والإعدادات تتبع ما فُتحت منه)
    public BpScreen TabScreen => Tabs.Contains(_screen) ? _screen : _from;
    public int TabIndex => Array.IndexOf(Tabs, TabScreen);
    public bool NavForward => _navForward;
    public bool IsGameOpen => _screen == BpScreen.Game;
    public bool IsSettingsOpen => _screen == BpScreen.Settings;
    public bool IsGuideOpen => _overlay == BpOverlay.Guide;
    public bool IsNotificationsOpen => _overlay == BpOverlay.Notifications;
    public bool IsUpdateOpen => _overlay == BpOverlay.Update;
    public bool IsLaunchOpen => _overlay == BpOverlay.Launch;

    /// مفتاح العنصر المركَّز الآن، أو null
    public string? FocusKey { get; private set; }

    // ===== الإدخال =====

    /// زر من يد التحكم أو لوحة المفاتيح. يعيد true إن تغيّر شيء.
    public bool Press(BpButton button)
    {
        if (Settings.SwapConfirmBack && button is BpButton.A or BpButton.B)
            button = button == BpButton.A ? BpButton.B : BpButton.A;

        var before = Snapshot();
        switch (button)
        {
            case BpButton.Up: Nav(0, -1); break;
            case BpButton.Down: Nav(0, 1); break;
            case BpButton.Left: Nav(IsRtl ? 1 : -1, 0); break;
            case BpButton.Right: Nav(IsRtl ? -1 : 1, 0); break;
            case BpButton.A: Activated?.Invoke(); Act(); break;
            case BpButton.B: Back(); break;
            // LB يسار ذراع التحكم فيحرك الاختيار لليسار، وRB لليمين، في اللغتين
            case BpButton.LB: if (_overlay == BpOverlay.None) CycleTab(IsRtl ? 1 : -1); break;
            case BpButton.RB: if (_overlay == BpOverlay.None) CycleTab(IsRtl ? -1 : 1); break;
            case BpButton.X:
                if (_overlay == BpOverlay.Notifications) DismissFocusedNotification();
                else if (_overlay == BpOverlay.None && _screen == BpScreen.Library) CyclePlatform();
                break;
            case BpButton.Y:
                if (_overlay == BpOverlay.Notifications) ClearNotifications();
                else if (_overlay == BpOverlay.None) GoTab(BpScreen.Search);
                break;
            case BpButton.Menu: ToggleGuide(); break;
            case BpButton.Notifications: if (_overlay == BpOverlay.None) OpenNotifications(); break;
        }

        SyncFocus();
        var changed = before != Snapshot();
        if (!changed && button is BpButton.Up or BpButton.Down or BpButton.Left or BpButton.Right)
        {
            var (dx, dy) = button switch
            {
                BpButton.Up => (0, -1),
                BpButton.Down => (0, 1),
                BpButton.Left => (-1, 0),
                _ => (1, 0),
            };
            Bumped?.Invoke(dx, dy);
        }
        return changed;
    }

    private string Snapshot() => string.Join("|", _screen, _overlay, FocusKey, _strip, _platform, _query.Length, _kbLang,
        _section, string.Join(",", SettingRows.Select(r => r.IsOn + ":" + r.Selected)));

    private void Nav(int dx, int dy)
    {
        switch (_overlay)
        {
            case BpOverlay.Guide: NavGuide(dy); return;
            case BpOverlay.Notifications: if (dy != 0) NavNotifications(dy); return;
            case BpOverlay.Update: _updIndex = Math.Clamp(_updIndex + dx, 0, UpdateButtons.Count - 1); return;
            case BpOverlay.Launch: if (LaunchState == BpLaunchState.Failed) _failIndex = Math.Clamp(_failIndex + dx, 0, FailButtons.Count - 1); return;
        }

        switch (_screen)
        {
            case BpScreen.Home: NavHome(dx, dy); break;
            case BpScreen.Library: NavLibrary(dx, dy); break;
            case BpScreen.Game: NavGame(dx, dy); break;
            case BpScreen.Search: NavSearch(dx, dy); break;
            case BpScreen.Settings: NavSettings(dx, dy); break;
        }
    }

    private void Act()
    {
        switch (_overlay)
        {
            case BpOverlay.Guide: RunGuide(_guideIndex); return;
            case BpOverlay.Notifications: RunNotificationAction(); return;
            case BpOverlay.Update: RunUpdateButton(_updIndex); return;
            case BpOverlay.Launch: if (LaunchState == BpLaunchState.Failed) RunFailButton(_failIndex); return;
        }

        switch (_screen)
        {
            case BpScreen.Home: ActHome(); break;
            case BpScreen.Library:
                if (_onFilters) { if (LibraryCards.Count > 0) _onFilters = false; }
                else if (FocusedLibraryGame is { } g) OpenGame(g);
                break;
            case BpScreen.Game: ActGame(); break;
            case BpScreen.Search: ActSearch(); break;
            case BpScreen.Settings: ActSettings(); break;
        }
    }

    private void Back()
    {
        if (_overlay != BpOverlay.None) { CloseOverlay(); return; }
        switch (_screen)
        {
            case BpScreen.Game: GoTab(_from); break;
            case BpScreen.Settings:
                if (_setCol == 1) _setCol = 0;
                else CloseSettings();
                break;
            case BpScreen.Search:
                if (_query.Length > 0) SetQuery(_query[..^1]);
                else GoTab(BpScreen.Home);
                break;
            case BpScreen.Library: GoTab(BpScreen.Home); break;
            // الرئيسي: B لا يفعل شيئًا، الخروج من القائمة أو الإعدادات
        }
    }

    // ===== التبويبات والصفحات =====

    public void GoTab(BpScreen tab)
    {
        if (!Tabs.Contains(tab)) return;
        _navForward = Array.IndexOf(Tabs, tab) > TabIndex;
        CloseOverlayQuiet();
        _screen = tab;
        _from = tab;
        Changed();
    }

    private void CycleTab(int delta) => GoTab(Tabs[(TabIndex + delta + Tabs.Length) % Tabs.Length]);

    public void OpenGame(BpGame game)
    {
        if (Tabs.Contains(_screen)) _from = _screen;
        CloseOverlayQuiet();
        Hero = game;
        _screen = BpScreen.Game;
        _gameCol = 0;
        _gameIndex = 0;
        BuildGameButtons();
        NewsItems.Clear();
        _ = LoadNewsAsync(game);
        Changed();
    }

    private async Task LoadNewsAsync(BpGame game)
    {
        try
        {
            var news = await _host.LoadNewsAsync(game);
            if (ReferenceEquals(_hero, game) && _screen == BpScreen.Game) SetNews(news);
        }
        catch (Exception ex) { HalawaHub.Core.Log.Error("big picture news failed", ex); }
    }

    public void OpenSettings()
    {
        if (Tabs.Contains(_screen)) _from = _screen;
        CloseOverlayQuiet();
        _screen = BpScreen.Settings;
        _setCol = 0;
        Changed();
    }

    public void CloseSettings() => GoTab(_from);

    public void ToggleGuide()
    {
        if (_overlay == BpOverlay.Guide) { CloseOverlay(); return; }
        CloseOverlayQuiet();
        _overlay = BpOverlay.Guide;
        _guideIndex = 0;
        RefreshGuideBadge();
        Changed();
    }

    public void CloseOverlay()
    {
        CloseOverlayQuiet();
        Changed();
    }

    private void CloseOverlayQuiet()
    {
        if (_overlay == BpOverlay.Launch) CancelLaunch();
        _overlay = BpOverlay.None;
    }

    // ===== التركيز =====

    private IEnumerable<BpItem> AllItems() =>
        HeroButtons.Cast<BpItem>().Concat(StripTabs).Concat(RowCards).Append(MoreCard)
            .Concat(LibraryCards).Concat(Filters).Concat(KeyRows.SelectMany(r => r)).Concat(Results)
            .Concat(GameButtons).Concat(NewsItems).Concat(SettingSections).Concat(SettingRows)
            .Concat(GuideItems).Concat(UpdateButtons).Concat(FailButtons).Concat(NotifItems);

    private void SyncFocus() => Changed();

    private string? ComputeFocusKey()
    {
        switch (_overlay)
        {
            case BpOverlay.Guide: return "guide." + _guideIndex;
            case BpOverlay.Notifications: return NotifItems.Count > 0 ? "nt." + _ntIndex : null;
            case BpOverlay.Update: return "upd." + _updIndex;
            case BpOverlay.Launch: return LaunchState == BpLaunchState.Failed ? "fail." + _failIndex : null;
        }

        return _screen switch
        {
            BpScreen.Home => _homeRow switch
            {
                0 => "hero." + _heroIndex,
                1 => "strip." + _strip,
                _ => _cardIndex >= RowCards.Count ? "more" : "card." + _cardIndex,
            },
            BpScreen.Library => _onFilters ? "flt." + _platform : LibraryCards.Count > 0 ? "lib." + _libIndex : null,
            BpScreen.Game => _gameCol == 0 ? "game." + _gameIndex : "news." + _gameIndex,
            BpScreen.Search => _kbZone == 0 ? "kb." + _kbRow + "." + _kbCol : "res." + _resIndex,
            BpScreen.Settings => _setCol == 0 ? "sec." + _section : "row." + _setRow,
            _ => null,
        };
    }

    /// كل تغيير يعيد رسم كل الروابط (المصفوفة صغيرة، والعناصر نفسها لا تُعاد)
    private void Changed()
    {
        UpdateLibraryWindow();
        FocusKey = ComputeFocusKey();
        foreach (var item in AllItems()) item.IsFocused = item.FocusKey == FocusKey;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void ShowToast(string text) => Toast?.Invoke(text);

    internal static string T(string key) => Loc.Instance[key];
    internal static string F(string key, params object[] args) => Loc.Instance.Format(key, args);
}
