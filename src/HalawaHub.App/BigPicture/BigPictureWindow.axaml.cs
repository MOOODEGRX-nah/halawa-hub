using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HalawaHub.App.BigPicture.Controls;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// نافذة الشاشة الكبيرة: ملء الشاشة على الشاشة الحالية، والتصميم 1920×1080 يتحجّم
/// داخل Viewbox فيبدو كما هو على 1080p و1440p و4K (النص يُرسم متجهًا فيبقى حادًا).
/// </summary>
public partial class BigPictureWindow : Window
{
    private const double Pitch = 204 + 26;   // عرض البطاقة والمسافة بينها
    private static readonly int[] RingPasses = { 0, 200, 450, 720 };

    private readonly BigPictureViewModel _vm;
    private readonly IGamepadSource? _pads;
    private readonly TranslateTransform _track = new();
    private readonly TranslateTransform _row = new();
    private readonly TranslateTransform _libSlide = new();
    private int _libFirst = -1;
    private readonly DispatcherTimer[] _ringTimers;
    private readonly DispatcherTimer _clock;
    private DispatcherTimer? _toastTimer;
    private bool _ringShown;
    private bool _flash;

    /// للمصمم فقط
    public BigPictureWindow() : this(null!, null) { }

    public BigPictureWindow(BigPictureViewModel vm, IGamepadSource? pads)
    {
        InitializeComponent();
        // لون التمييز وثيم OLED من الإعدادات › المظهر (6.4)
        BpPalette.Use(ThemeManager.CurrentAccent, ThemeManager.CurrentTheme);
        BpPalette.Apply(Resources);
        _vm = vm;
        _pads = pads;
        DataContext = vm;

        Track.RenderTransform = _track;
        RowStrip.RenderTransform = _row;
        LibraryGrid.RenderTransform = _libSlide;
        ApplyMotion();

        _ringTimers = RingPasses.Select((ms, i) =>
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, ms)) };
            var first = i == 0;
            t.Tick += (_, _) => { t.Stop(); PlaceRing(first && _flash); if (first) _flash = false; };
            return t;
        }).ToArray();

        // الساعة: تحديث كل نصف دقيقة فقط، لا مؤقت سريع
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clock.Tick += (_, _) => UpdateClock();
        UpdateClock();

        if (vm == null) return;

        vm.PropertyChanged += OnVmChanged;
        vm.Bumped += OnBumped;
        vm.Activated += OnActivated;
        vm.Toast += ShowToast;
        // الخلفية: معاينات البطاقات، وإعادة الضبط عند تغييرها من الإعدادات
        vm.WallPreview = id => _previews.TryGetValue(id, out var bmp) ? bmp : _previews[id] = Wallpaper.WallpaperView.Preview(id, BpPalette.Accent, BpPalette.Oled);
        vm.WallpaperChanged += ApplyWallpaper;
        Opened += (_, _) => ApplyWallpaper();
        // الحافظة ومنتقي الملفات في النافذة: نسخ المسار، لصق المفتاح، صورة غلاف من الجهاز
        vm.CopyRequested += text => _ = Clipboard?.SetTextAsync(text);
        vm.PasteKeyRequested += () => SafeUi.Run(async () =>
        {
            var text = Clipboard is { } cb ? await cb.GetTextAsync() : null;
            vm.PasteKey(text);
        }, "paste SteamGridDB key");
        vm.PickCoverFileRequested += game => SafeUi.Run(async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = Loc.Instance["chooseCover"],
                AllowMultiple = false,
                FileTypeFilter = new[] { Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll },
            });
            if (files.Count > 0) vm.SetOwnCover(game, files[0].Path.LocalPath);
        }, "pick cover in Big Picture");
        Loc.Instance.LanguageChanged += OnLanguageChanged;

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        if (pads != null)
        {
            pads.ButtonPressed += OnPadButton;
            pads.PadChanged += OnPadChanged;
            vm.SetPad(pads.Current);
        }

        Opened += (_, _) =>
        {
            _clock.Start();
            _pads?.SetActive(true);
            SyncView(animate: false);
            PlayIntro();
        };
        Activated += (_, _) => _pads?.SetActive(true);
        // لعبة أمام النافذة: قراءة بطيئة، والأزرار لا تحرّك الشاشة الكبيرة خلفها
        Deactivated += (_, _) => _pads?.SetActive(false);
        Closed += (_, _) => Detach();
    }

    // ===== الإدخال =====

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (KeyboardMap.Map(e.Key) is not { } button) return;
        e.Handled = true;
        if (IntroRunning) { SkipIntro(); return; }
        _vm.Press(button);
    }

    private void OnPadButton(BpButton button) => Dispatcher.UIThread.Post(() =>
    {
        // اليد تبقى ترسل وهي في الخلفية (لا نافذة SDL): لا نتحرك خلف لعبة تعمل
        if (IsActive && IntroRunning) SkipIntro();
        else if (IsActive) _vm.Press(button);
        else if (Environment.GetEnvironmentVariable("HALAWAHUB_BP_INPUTLOG") == "1") HalawaHub.Core.Log.Info("gamepad button ignored: Big Picture is not the active window");
    });

    private void OnPadChanged(PadInfo? pad) => Dispatcher.UIThread.Post(() => _vm.SetPad(pad));

    // ===== مزامنة العرض مع الحالة =====

    /// تبدّل اللقطة كل 4.5 ثانية يغيّر صورتها فقط: لا مزامنة للنافذة كلها ولا نقرة على الخلفية (قاعدة الخمول)
    private static readonly System.Collections.Generic.HashSet<string> ShotOnly = new()
    {
        nameof(BigPictureViewModel.ShotA), nameof(BigPictureViewModel.ShotB), nameof(BigPictureViewModel.ShowShotB), nameof(BigPictureViewModel.ShotCaption),
    };

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && ShotOnly.Contains(e.PropertyName)) return;
        SyncView(animate: true);
    }

    private void SyncView(bool animate)
    {
        // الصفحات: الإزاحة سالبة دائمًا، والعربية تعكسها المرآة تلقائيًا
        _track.X = -_vm.TabIndex * _vm.CanvasWidth;
        _row.X = -_vm.RowOffsetCards * Pitch;
        // المكتبة: الصفوف تتبدل فورًا، ثم تنزلق من مكانها القديم (كأنها تمررت)
        var first = _vm.LibraryFirstRow;
        if (first != _libFirst)
        {
            if (animate && _libFirst >= 0 && MotionSettings.Enabled && Math.Abs(first - _libFirst) <= 3)
            {
                _libSlide.Transitions = null;
                _libSlide.Y = (first - _libFirst) * 340.0;
                _libSlide.Transitions = new Transitions { new DoubleTransition { Property = TranslateTransform.YProperty, Duration = TimeSpan.FromSeconds(0.35), Easing = BpSprings.Glide } };
                _libSlide.Y = 0;
            }
            _libFirst = first;
        }
        ScheduleRing();
        if (animate) Animate();
    }

    private void ApplyMotion()
    {
        var on = MotionSettings.Enabled;
        _track.Transitions = on ? new Transitions { new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromSeconds(0.6), Easing = BpSprings.Track } } : null;
        _row.Transitions = on ? new Transitions { new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromSeconds(0.55), Easing = BpSprings.Glide } } : null;
        if (!on) Ring.Transitions = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _vm.OnLanguageChanged();
        SyncView(animate: false);
    }

    // ===== حلقة التركيز =====

    /// العنصر يتحرك أثناء حركته (رفع البطاقة، انزلاق الصفحة)، فنعيد القياس على دفعات كما يفعل التصميم
    private void ScheduleRing()
    {
        // أول قياس بعد التخطيط مباشرة، ثم ثلاث إعادات أثناء الحركة
        Dispatcher.UIThread.Post(() => { PlaceRing(_flash); _flash = false; }, DispatcherPriority.Loaded);
        foreach (var t in _ringTimers.Skip(1)) { t.Stop(); t.Start(); }
    }

    private Control? FindFocused()
    {
        var key = _vm.FocusKey;
        if (key == null) return null;
        // نبحث في الجزء الذي يملك المفتاح فقط: البحث في كل النافذة كان يأخذ نحو 40ms مع كل ضغطة
        Visual scope = key.Split('.')[0] switch
        {
            "card" or "more" or "hero" or "strip" => HomePage,
            "lib" => LibraryGrid,
            "flt" => LibraryPage,
            "kb" => _vm.IsEntryOpen ? EntryPanel : SearchPage,
            "res" => SearchPage,
            "sg" => SgdbPage,
            "deal" => DealsPage,
            "game" or "news" => GamePage,
            "row" or "sec" => SettingsLayer,
            "upd" => UpdatePanel,
            "fail" => LaunchPanel,
            "nt" => NotifSheet,
            _ => Root,
        };
        return scope.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => BpFocus.GetKey(c) == key && c.IsEffectivelyVisible && c.Bounds.Width > 0);
    }

    /// <summary>
    /// مكان العنصر المركَّز بعد انتهاء كل الحركات: من التخطيط وحده (بلا تحويلات الرسم التي
    /// قد تكون في منتصف حركتها)، ثم الإزاحات النهائية المعروفة (الصفحة، صف البطاقات، رفع
    /// العنصر المركَّز). القياس في منتصف الحركة كان يجعل الحلقة تلحق العنصر على دفعات.
    /// </summary>
    internal Rect? FocusRect()
    {
        if (FindFocused() is not { } target) return null;
        double x = 0, y = 0, extraX = 0;
        var tw = target.Bounds.Width;
        Visual? v = target;
        while (v != null && v != Root)
        {
            // عنصر باتجاه غير اتجاه أبيه (حروف إنجليزية داخل الواجهة العربية) يُرسم معكوسًا داخل
            // حدوده، فمكان العنصر فيه يُعكس قبل الصعود للأب، وإلا وقعت الحلقة على المفتاح المقابل
            if (v is Control c && v.GetVisualParent() is Control p && c.FlowDirection != p.FlowDirection)
                x = v.Bounds.Width - x - tw;
            x += v.Bounds.X;
            y += v.Bounds.Y;
            if (ReferenceEquals(v, RowStrip)) extraX += -_vm.RowOffsetCards * Pitch;
            // صفوف العروض تنزلق أفقيًا، والقائمة كلها تصعد عند الصف الثالث
            if (v is ItemsControl { DataContext: BpDealRow dealRow } ic && ic.Classes.Contains("dealrow")) extraX += dealRow.ShiftX;
            if (ReferenceEquals(v, DealRowsList)) y += _vm.DealsShiftY;
            if (ReferenceEquals(v, SgdbGrid)) y += _vm.SgdbShiftY;
            if (ReferenceEquals(v, Track)) extraX += -_vm.TabIndex * _vm.CanvasWidth;
            v = v.GetVisualParent();
        }
        if (v == null) return null;
        var (scale, dy) = FocusedTransform(target);
        var w = target.Bounds.Width;
        var h = target.Bounds.Height;
        return new Rect(x + extraX - w * (scale - 1) / 2, y + dy - h * (scale - 1) / 2, w * scale, h * scale);
    }

    /// تكبير العنصر المركَّز ورفعه كما في أنماطه (.c.f و.btn.f و.key.f و.qsr.f و.ntr.f)
    private static (double Scale, double Dy) FocusedTransform(Control c) =>
        c.Classes.Contains("bpcard") ? (1.06, -10)
        : c.Classes.Contains("wide") ? (1, 0)
        : c.Classes.Contains("key") ? (1.08, 0)
        : c.Classes.Contains("btn") ? (1.05, 0)
        : c.Classes.Contains("qsr") || c.Classes.Contains("ntr") ? (1.02, 0)
        : (1, 0);

    private void PlaceRing(bool flash)
    {
        const double pad = 7;
        var target = FindFocused();
        if (target == null || FocusRect() is not { } r)
        {
            Ring.Opacity = 0;
            _ringShown = false;
            return;
        }

        var scale = target.Bounds.Width > 0 ? r.Width / target.Bounds.Width : 1;
        var radius = BpFocus.GetRadius(target) * scale + pad;

        // أول ظهور: بلا انتقال، حتى لا تطير الحلقة من الزاوية
        var transitions = Ring.Transitions;
        if (!_ringShown) Ring.Transitions = null;
        Canvas.SetLeft(Ring, r.X - pad);
        Canvas.SetTop(Ring, r.Y - pad);
        Ring.Width = r.Width + pad * 2;
        Ring.Height = r.Height + pad * 2;
        Ring.CornerRadius = new CornerRadius(radius);
        Ring.Opacity = 1;
        MoveLight(r);
        if (!_ringShown) { Ring.Transitions = transitions; _ringShown = true; }

        if (flash && MotionSettings.Enabled) _ = FlashRing();
    }

    private async System.Threading.Tasks.Task FlashRing()
    {
        var anim = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(0.45),
            Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Avalonia.Styling.Setter(OpacityProperty, 0.9) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Avalonia.Styling.Setter(OpacityProperty, 0.0) } },
            },
        };
        await anim.RunAsync(RingFlash);
    }

    private async void OnBumped(int dx, int dy)
    {
        if (!MotionSettings.Enabled || !_ringShown) return;
        // دفعة صغيرة نحو الحافة ثم رجوع (280ms)
        var rtl = _vm.IsRtl ? -1 : 1;
        var shift = new TranslateTransform();
        Ring.RenderTransform = shift;
        var anim = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(280),
            Easing = BpSprings.Ring,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Avalonia.Styling.Setter(TranslateTransform.XProperty, 0.0), new Avalonia.Styling.Setter(TranslateTransform.YProperty, 0.0) } },
                new KeyFrame { Cue = new Cue(0.5), Setters = { new Avalonia.Styling.Setter(TranslateTransform.XProperty, dx * 10.0 * rtl), new Avalonia.Styling.Setter(TranslateTransform.YProperty, dy * 10.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Avalonia.Styling.Setter(TranslateTransform.XProperty, 0.0), new Avalonia.Styling.Setter(TranslateTransform.YProperty, 0.0) } },
            },
        };
        try { await anim.RunAsync(Ring); } catch (Exception ex) { HalawaHub.Core.Log.Error("bump failed", ex); }
    }

    // ===== الساعة والرسالة =====

    private void UpdateClock()
    {
        var now = DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Clock.Text = now;
        GuideClock.Text = now;
    }

    private void ShowToast(string text)
    {
        ToastText.Text = text;
        ToastBox.IsVisible = true;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) => { _toastTimer?.Stop(); ToastBox.IsVisible = false; };
        _toastTimer.Start();
        _ = MotionSettings.AnimateAsync(ToastBox, TimeSpan.FromSeconds(0.4), BpSprings.Lift, 0, 0.92, -20);
    }

    // ===== الفأرة =====

    private static T? Item<T>(object? sender) where T : class => (sender as Control)?.DataContext as T;

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpCardItem>(sender) is not { } item) return;
        if (item.FocusKey.StartsWith("card.")) _vm.ClickCard(item.Index);
        else if (item.FocusKey.StartsWith("lib.")) _vm.ClickLibraryCard(item.Index);
        else if (item.FocusKey.StartsWith("res.")) _vm.ClickResult(item.Index);
    }

    private void OnMoreClick(object? sender, RoutedEventArgs e) => _vm.ClickCard(_vm.RowCards.Count);

    private void OnDealClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpDealItem>(sender) is { } item) _vm.ClickDeal(item.Row, item.Index);
    }

    private void OnHeroButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickHeroButton(_vm.HeroButtons.IndexOf(item));
    }

    private void OnStripClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickStrip(_vm.StripTabs.IndexOf(item));
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is string tag && int.TryParse(tag, out var i)) _vm.GoTab(BigPictureViewModel.Tabs[i]);
    }

    private void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpFilterItem>(sender) is { } item) _vm.SetPlatform(item.Id);
    }

    private void OnKeyClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is not { } item) return;
        for (var r = 0; r < _vm.KeyRows.Count; r++)
        {
            var c = _vm.KeyRows[r].IndexOf(item);
            if (c < 0) continue;
            if (_vm.IsEntryOpen) _vm.ClickEntryKey(r, c);
            else _vm.ClickKey(r, c);
            return;
        }
    }

    private void OnSgdbClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpSgdbItem>(sender) is { } item) _vm.ClickSgdb(item.Index);
    }

    private void OnGameOptionClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickGameOption(_vm.GameOptions.IndexOf(item));
    }

    private void OnGameButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickGameButton(_vm.GameButtons.IndexOf(item));
    }

    private void OnNewsClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickNews(_vm.NewsItems.IndexOf(item));
    }

    private void OnSectionClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickSection(_vm.SettingSections.IndexOf(item));
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpSettingRow>(sender) is { } item) _vm.ClickRow(_vm.SettingRows.IndexOf(item));
    }

    private void OnUpdateButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickUpdateButton(_vm.UpdateButtons.IndexOf(item));
    }

    private void OnFailButtonClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickFailButton(_vm.FailButtons.IndexOf(item));
    }

    private void OnGuideItemClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpButtonItem>(sender) is { } item) _vm.ClickGuide(_vm.GuideItems.IndexOf(item));
    }

    /// الضغط خارج النافذة يغلقها (فقط على الخلفية نفسها)
    private void OnSettingsScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, SettingsLayer)) _vm.CloseSettings();
    }

    private void OnGuideScrimPressed(object? sender, PointerPressedEventArgs e) => _vm.CloseOverlay();

    private void OnClearNotificationsClick(object? sender, RoutedEventArgs e) => _vm.ClickClearNotifications();

    private void OnNotificationPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Item<BpNotifItem>(sender) is { } item) _vm.ClickNotification(item.Index);
    }

    private void OnNotificationActionClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpNotifItem>(sender) is { } item) _vm.ClickNotificationAction(item.Index);
        e.Handled = true;
    }

    private void OnNotificationDismissClick(object? sender, RoutedEventArgs e)
    {
        if (Item<BpNotifItem>(sender) is { } item) _vm.ClickNotificationDismiss(item.Index);
        e.Handled = true;
    }

    private void OnIncomingPressed(object? sender, PointerPressedEventArgs e) => _vm.OpenIncoming();

    private void OnGuideClick(object? sender, RoutedEventArgs e) => _vm.ToggleGuide();
    private void OnBellClick(object? sender, RoutedEventArgs e) => _vm.OpenNotifications();
    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.IsSettingsOpen) _vm.CloseSettings();
        else _vm.OpenSettings();
    }

    // ===== النافذة =====

    /// ملء الشاشة على الشاشة التي فيها النافذة العادية (أو الشاشة الأساسية)
    public void ShowOn(Window? owner)
    {
        var screen = owner != null ? owner.Screens.ScreenFromWindow(owner) : Screens.Primary;
        screen ??= Screens.Primary;
        if (screen != null)
        {
            Position = screen.Bounds.Position;
            Width = screen.Bounds.Width / screen.Scaling;
            Height = screen.Bounds.Height / screen.Scaling;
        }
        Show();
        WindowState = WindowState.FullScreen;
        Activate();
    }

    private void Detach()
    {
        _clock.Stop();
        foreach (var t in _ringTimers) t.Stop();
        _toastTimer?.Stop();
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmChanged;
            _vm.Bumped -= OnBumped;
            _vm.Toast -= ShowToast;
        }
        if (_pads != null)
        {
            _pads.ButtonPressed -= OnPadButton;
            _pads.PadChanged -= OnPadChanged;
            _pads.SetActive(false);
        }
    }
}
