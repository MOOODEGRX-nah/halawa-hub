using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using HalawaHub.App.BigPicture.Controls;
using HalawaHub.App.Services;
using HalawaHub.Core;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// حركة الشاشة الكبيرة (المرحلة 3): كل حركة تبدأ مع تغيير وتنتهي، ولا شيء يدور وهي ساكنة.
/// "تقليل الحركة" يوقفها كلها (MotionSettings.Enabled).
/// </summary>
public partial class BigPictureWindow
{
    private int _heroV = -1, _rowV = -1, _libV = -1, _secV = -1, _resV = -1;
    private BpScreen _lastScreen = BpScreen.Home;
    private BpOverlay _lastOverlay = BpOverlay.None;
    private string _lastQuery = "";
    private int _bellV, _incomingV;

    /// دخول ما تغيّر فقط، مرة واحدة لكل تغيير
    private void Animate()
    {
        var vm = _vm;
        if (vm.HeroVersion != _heroV) { if (_heroV >= 0 && vm.Screen == BpScreen.Home) AnimateHero(); _heroV = vm.HeroVersion; }
        if (vm.RowVersion != _rowV) { if (_rowV >= 0) Stagger(RowItems, 45); _rowV = vm.RowVersion; }
        if (vm.LibraryVersion != _libV) { if (_libV >= 0) StaggerLibrary(); _libV = vm.LibraryVersion; }
        if (vm.SectionVersion != _secV)
        {
            if (_secV >= 0) _ = MotionSettings.AnimateAsync(SettingRowsList, TimeSpan.FromSeconds(0.4), MotionSettings.EnterEasing, 0, 1, vm.SectionForward ? 16 : -16);
            _secV = vm.SectionVersion;
        }
        if (vm.ResultsVersion != _resV) { if (_resV >= 0) Stagger(ResultsList, 70, 50, -18); _resV = vm.ResultsVersion; }

        if (vm.Screen != _lastScreen)
        {
            if (vm.Screen == BpScreen.Game)
            {
                _ = MotionSettings.AnimateAsync(GamePage, TimeSpan.FromSeconds(0.5), BpSprings.Track, 0, 0.985, 24);
                SlideIn(GameSide, 36, 0.6, 0.1);
            }
            else if (vm.Screen == BpScreen.Settings)
                _ = MotionSettings.AnimateAsync(SettingsDialog, TimeSpan.FromSeconds(0.42), MotionSettings.DialogEasing, 0, 0.94, 18);
            _lastScreen = vm.Screen;
        }

        if (vm.Overlay != _lastOverlay)
        {
            switch (vm.Overlay)
            {
                case BpOverlay.Update: _ = MotionSettings.AnimateAsync(UpdatePanel, TimeSpan.FromSeconds(0.42), MotionSettings.DialogEasing, 0, 0.94, 18); break;
                case BpOverlay.GameOptions: _ = MotionSettings.AnimateAsync(GameOptionsPanel, TimeSpan.FromSeconds(0.42), MotionSettings.DialogEasing, 0, 0.94, 18); break;
                case BpOverlay.Launch: _ = MotionSettings.AnimateAsync(LaunchPanel, TimeSpan.FromSeconds(0.42), MotionSettings.DialogEasing, 0, 0.94, 18); break;
                case BpOverlay.Guide: SlideIn(GuideSheet, -520, 0.5, 0, BpSprings.Track); break;
                case BpOverlay.Notifications: SlideIn(NotifSheet, 660, 0.5, 0, BpSprings.Track); StaggerNotifications(); break;
            }
            _lastOverlay = vm.Overlay;
        }

        if (vm.BellVersion != _bellV) { _bellV = vm.BellVersion; BellShake(); }
        if (vm.IncomingVersion != _incomingV) { _incomingV = vm.IncomingVersion; if (vm.Incoming != null) ShowIncoming(); }

        // لا نتائج: الأيقونة تهتز مع كل حرف لا يطابق شيئًا، و"?" يقفز مرة
        if (vm.Query != _lastQuery)
        {
            if (vm.NoMatch) { Shake(SearchIcon); Pop(QBadge); }
            _lastQuery = vm.Query;
        }
    }

    private void AnimateHero()
    {
        _ = MotionSettings.AnimateAsync(HeroArtFill, TimeSpan.FromSeconds(0.8), BpSprings.Glide, 0, 1.05, 0);
        var i = 0;
        foreach (var child in HeroPanel.Children)
            _ = MotionSettings.AnimateAsync(child, TimeSpan.FromSeconds(0.55), BpSprings.Glide, 0, 1, 14, delay: TimeSpan.FromMilliseconds(Math.Min(i++, 2) * 40));
    }

    /// البطاقات الظاهرة تدخل واحدة بعد الأخرى (cardIn)
    private static void Stagger(ItemsControl list, int stepMs, int startMs = 0, double fromY = 18)
    {
        if (!MotionSettings.Enabled) return;
        for (var i = 0; i < list.ItemCount; i++)
            if (list.ContainerFromIndex(i) is { } c)
                _ = MotionSettings.AnimateAsync(c, TimeSpan.FromSeconds(0.5), MotionSettings.EnterEasing, 0, 0.97, fromY, delay: TimeSpan.FromMilliseconds(startMs + i * stepMs));
    }

    private void StaggerLibrary()
    {
        if (!MotionSettings.Enabled) return;
        var n = 0;
        foreach (var card in LibraryGrid.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("bpcard")).Take(21))
            _ = MotionSettings.AnimateAsync(card, TimeSpan.FromSeconds(0.5), MotionSettings.EnterEasing, 0, 0.97, 18, delay: TimeSpan.FromMilliseconds(n++ * 30));
    }

    private static async void SlideIn(Visual target, double fromX, double seconds, double delay, Easing? easing = null)
    {
        if (!MotionSettings.Enabled) return;
        var t = new TranslateTransform();
        target.RenderTransform = t;
        var anim = new Animation
        {
            Duration = TimeSpan.FromSeconds(seconds),
            Delay = TimeSpan.FromSeconds(delay),
            Easing = easing ?? BpSprings.Glide,
            FillMode = FillMode.Both,
            Children = { Frame(0, (TranslateTransform.XProperty, fromX)), Frame(1, (TranslateTransform.XProperty, 0.0)) },
        };
        try { await anim.RunAsync(target); } catch (Exception ex) { Log.Error("slide failed", ex); }
    }

    /// sbShake: اهتزاز ودوران قصير يخفت (0.9 ثانية)
    private static async void Shake(Visual target)
    {
        if (!MotionSettings.Enabled) return;
        var rotate = new RotateTransform();
        var move = new TranslateTransform();
        target.RenderTransformOrigin = RelativePoint.Center;
        target.RenderTransform = new TransformGroup { Children = { rotate, move } };
        (double Cue, double X, double A)[] k = { (0, 0, 0), (0.12, 7, 17), (0.26, -6, -14), (0.40, 5, 10), (0.55, -3, -6), (0.70, 2, 3), (1, 0, 0) };
        var a = new Animation { Duration = TimeSpan.FromSeconds(0.9) };
        var r = new Animation { Duration = TimeSpan.FromSeconds(0.9) };
        foreach (var f in k)
        {
            a.Children.Add(Frame(f.Cue, (TranslateTransform.XProperty, f.X)));
            r.Children.Add(Frame(f.Cue, (RotateTransform.AngleProperty, f.A)));
        }
        try { await Task.WhenAll(a.RunAsync(target), r.RunAsync(target)); } catch (Exception ex) { Log.Error("shake failed", ex); }
    }

    /// qIn: الشارة تظهر بقفزة (بدل الطفو الدائم في التصميم، حتى لا تتحرك وهي ساكنة)
    private static async void Pop(Visual target)
    {
        if (!MotionSettings.Enabled) return;
        var s = new ScaleTransform();
        target.RenderTransformOrigin = RelativePoint.Center;
        target.RenderTransform = s;
        var anim = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.4),
            Delay = TimeSpan.FromSeconds(0.12),
            Easing = BpSprings.Lift,
            FillMode = FillMode.Both,
            Children = { Frame(0, (ScaleTransform.ScaleXProperty, 0.0), (ScaleTransform.ScaleYProperty, 0.0)), Frame(1, (ScaleTransform.ScaleXProperty, 1.0), (ScaleTransform.ScaleYProperty, 1.0)) },
        };
        try { await anim.RunAsync(target); } catch (Exception ex) { Log.Error("pop failed", ex); }
    }

    /// bellA: الجرس يهتز عند وصول إشعار
    private async void BellShake()
    {
        if (!MotionSettings.Enabled) return;
        var r = new RotateTransform();
        BellIcon.RenderTransformOrigin = new RelativePoint(0.5, 0.08, RelativeUnit.Relative);
        BellIcon.RenderTransform = r;
        var anim = new Animation { Duration = TimeSpan.FromSeconds(0.9), Easing = new CubicEaseOut() };
        foreach (var (cue, a) in new[] { (0.0, 0.0), (0.12, -18.0), (0.26, 15.0), (0.40, -11.0), (0.54, 7.0), (0.68, -3.0), (1.0, 0.0) })
            anim.Children.Add(Frame(cue, (RotateTransform.AngleProperty, a)));
        try { await anim.RunAsync(BellIcon); } catch (Exception ex) { Log.Error("bell failed", ex); }
    }

    /// ntoIn ثم شريط التقدم 5 ثوانٍ (يتوقف مع الإشعار، لا حلقة)
    private async void ShowIncoming()
    {
        SlideIn(IncomingToast, 660, 0.6, 0, BpSprings.Open);
        if (!MotionSettings.Enabled) return;
        var s = new ScaleTransform();
        IncomingBar.RenderTransform = s;
        var bar = new Animation { Duration = TimeSpan.FromSeconds(5), FillMode = FillMode.Forward, Children = { Frame(0, (ScaleTransform.ScaleXProperty, 1.0)), Frame(1, (ScaleTransform.ScaleXProperty, 0.0)) } };
        try { await bar.RunAsync(IncomingBar); } catch (Exception ex) { Log.Error("incoming bar failed", ex); }
    }

    private void StaggerNotifications()
    {
        if (!MotionSettings.Enabled) return;
        var i = 0;
        foreach (var row in NotifSheet.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("ntr")))
            _ = MotionSettings.AnimateAsync(row, TimeSpan.FromSeconds(0.45), MotionSettings.EnterEasing, 0, 1, 0, delay: TimeSpan.FromMilliseconds(60 + i++ * 60));
    }

    /// A: وميض الحلقة، علامة الضغط، وموجة عبر نقاط الشبكة
    private void OnActivated()
    {
        _flash = true;
        if (!MotionSettings.Enabled || FocusRect() is not { } r) return;
        var c = r.Center;
        foreach (var gl in Lights.Where(g => g.IsEffectivelyVisible)) gl.Wave(c.X, c.Y);
        TapMark(r);
    }

    /// الضوء يتبع مركز العنصر المركَّز
    private BpGridLight[]? _lights;
    /// أضواء الصفحات الأربع (تُجمع مرة واحدة: البحث في كل النافذة مع كل ضغطة مكلف)
    private BpGridLight[] Lights => _lights ??= Root.GetVisualDescendants().OfType<BpGridLight>().Where(g => !ReferenceEquals(g, IntroGrid)).ToArray();

    private void MoveLight(Rect r)
    {
        var c = r.Center;
        foreach (var gl in Lights)
        {
            gl.LightX = c.X;
            gl.LightY = c.Y;
        }
    }

    /// علامة الضغط تأخذ مكان الحلقة وشكلها: وميض داخلها ثم إطار يتسع قليلًا ويختفي
    private async void TapMark(Rect r)
    {
        const double pad = 7;
        var x = r.X - pad; var y = r.Y - pad; var w = r.Width + pad * 2; var h = r.Height + pad * 2;
        foreach (var b in new[] { TapM, TapR })
        {
            Canvas.SetLeft(b, x); Canvas.SetTop(b, y);
            b.Width = w; b.Height = h;
            b.CornerRadius = Ring.CornerRadius;
        }
        var ms = new ScaleTransform(); TapM.RenderTransform = ms;
        var rs = new ScaleTransform(); TapR.RenderTransform = rs;
        // تكبير نسبي صغير: المربع كبير بخلاف الدائرة، فتكبيره 2.3 مرة كان سيغطي الشاشة
        var mark = new Animation { Duration = TimeSpan.FromMilliseconds(480), Easing = new CubicEaseOut(), Children = { Tap(0, 0, 1.03), Tap(0.35, 0.9, 0.98), Tap(0.7, 0.6, 1), Tap(1, 0, 1) } };
        var ring = new Animation { Duration = TimeSpan.FromMilliseconds(420), Delay = TimeSpan.FromMilliseconds(160), Easing = new CubicEaseOut(), Children = { Tap(0, 0.8, 1), Tap(1, 0, 1.1) } };
        try { await Task.WhenAll(mark.RunAsync(TapM), ring.RunAsync(TapR)); } catch (Exception ex) { Log.Error("tap mark failed", ex); }
        TapM.Opacity = 0;
        TapR.Opacity = 0;
    }

    private static KeyFrame Tap(double cue, double opacity, double scale) =>
        Frame(cue, (OpacityProperty, opacity), (ScaleTransform.ScaleXProperty, scale), (ScaleTransform.ScaleYProperty, scale));

    private static KeyFrame Frame(double cue, params (AvaloniaProperty Property, double Value)[] setters)
    {
        var kf = new KeyFrame { Cue = new Cue(cue) };
        foreach (var (p, v) in setters) kf.Setters.Add(new Setter(p, v));
        return kf;
    }
}
