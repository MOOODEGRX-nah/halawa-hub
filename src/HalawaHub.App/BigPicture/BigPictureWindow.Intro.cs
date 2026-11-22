using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using HalawaHub.App.BigPicture.Controls;
using HalawaHub.App.Services;
using HalawaHub.Core;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// بداية الشاشة الكبيرة (نحو 3 ثوانٍ، مثل بداية Big Picture في Steam):
/// الشبكة تُرسم من المركز وتضيء نقاطها، شعاع ضوء يعبر الشاشة، الشعار يظهر كبيرًا بتوهج
/// ولمعة تمر عليه، الاسم يصعد حرفًا حرفًا مع "الشاشة الكبيرة" وخط ينفتح من الوسط، ثم
/// عبور: الشعار يكبر ويتلاشى والرئيسي يدخل من خلفه. أي زر يتخطاها، و"تقليل الحركة" يلغيها.
/// التوقيت بـ Task.Delay لا بانتظار الحركات (انتظارها لا ينتهي في بعض البيئات).
/// </summary>
public partial class BigPictureWindow
{
    private CancellationTokenSource? _introCts;
    private bool _introEnding;
    internal bool IntroRunning { get; private set; }
    /// مدة بقاء الشعار بعد اكتماله (الاختبارات تطيلها لتصوّر البداية وتجرّب التخطي)
    internal static TimeSpan IntroHold { get; set; } = TimeSpan.FromMilliseconds(900);

    private static readonly Easing Out = new CssBezierEasing(0.22, 1, 0.36, 1);
    private static readonly Easing Spring = new CssBezierEasing(0.34, 1.56, 0.64, 1);
    private static readonly Easing InOut = new CssBezierEasing(0.65, 0, 0.35, 1);
    private static readonly Easing Linear = new LinearEasing();

    private async void PlayIntro()
    {
        if (!MotionSettings.Enabled) { Intro.IsVisible = false; return; }

        IntroRunning = true;
        Intro.IsVisible = true;
        Intro.Opacity = 1;
        foreach (var v in new Visual[] { IntroBg, IntroGlow, IntroGrid, IntroLogo, IntroWords, IntroLine }) v.ClearValue(OpacityProperty);
        IntroLogo.Opacity = 0;
        BuildIntroName();
        var cts = _introCts = new CancellationTokenSource();
        var cx = Root.Bounds.Width / 2;
        try
        {
            // 1) الشبكة تُرسم من المركز للخارج، والضوء خلف الشعار
            IntroGrid.Transitions = null;
            IntroGrid.LightX = cx;
            IntroGrid.LightY = 410;
            _ = Animate(IntroGrid, 1.1, 0, InOut, (BpGridLight.RevealProperty, 0.0, 1400.0));
            _ = Animate(IntroGlow, 1.4, 0.1, Out, (OpacityProperty, 0.0, 1.0), (ScaleTransform.ScaleXProperty, 0.55, 1.0), (ScaleTransform.ScaleYProperty, 0.55, 1.0));

            // 2) شعاع ضوء مائل يعبر الشاشة
            IntroStreak.RenderTransform = new TransformGroup { Children = { new RotateTransform(18), new TranslateTransform(-1300, 0) } };
            _ = Animate(IntroStreak, 1.0, 0.25, InOut, (TranslateTransform.XProperty, -1300.0, 1300.0));
            _ = Animate(IntroStreak, 1.0, 0.25, Linear, (OpacityProperty, 0.0, 1.0), (OpacityProperty, 1.0, 0.0));

            // 3) الشعار يظهر كبيرًا بتوهج وارتداد، ثم لمعة تمر عليه
            await Task.Delay(380, cts.Token);
            ElasticIn(IntroLogo);
            IntroShine.RenderTransform = new TransformGroup { Children = { new RotateTransform(20), new TranslateTransform(-280, 0) } };
            _ = Animate(IntroShine, 0.8, 0.75, InOut, (TranslateTransform.XProperty, -280.0, 280.0));
            await Task.Delay(320, cts.Token);
            IntroGrid.Wave(cx, 410);

            // 4) الاسم حرفًا حرفًا، ثم "الشاشة الكبيرة" والخط من الوسط
            var i = 0;
            foreach (var child in IntroName.Children)
                _ = Animate(child, 0.55, 0.04 * i++, Spring, (OpacityProperty, 0.0, 1.0), (TranslateTransform.YProperty, 26.0, 0.0));
            _ = Animate(IntroSub, 0.6, 0.45, Out, (OpacityProperty, 0.0, 1.0), (TranslateTransform.YProperty, 14.0, 0.0));
            _ = Animate(IntroLine, 0.8, 0.5, InOut, (OpacityProperty, 0.0, 1.0), (ScaleTransform.ScaleXProperty, 0.0, 1.0));

            await Task.Delay(1100, cts.Token);
            await Task.Delay(IntroHold, cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Error("intro failed", ex); }
        await EndIntro(skipped: cts.IsCancellationRequested);
    }

    /// كل حرف عنصر مستقل حتى يصعد وحده
    private void BuildIntroName()
    {
        IntroName.Children.Clear();
        foreach (var ch in "Halawa Hub")
            IntroName.Children.Add(new TextBlock
            {
                Text = ch == ' ' ? " " : ch.ToString(),
                FontFamily = new FontFamily("fonts:Inter#Inter"),
                FontWeight = FontWeight.ExtraBold,
                FontSize = 76,
                Foreground = Brushes.White,
                Opacity = 0,
            });
    }

    /// أي زر أثناء البداية يتخطاها ولا يُنفَّذ
    private void SkipIntro() => _introCts?.Cancel();

    private async Task EndIntro(bool skipped)
    {
        if (!IntroRunning || _introEnding) return;
        _introEnding = true;
        var t = skipped ? 0.5 : 0.85;

        // 5) الشعار يطير إلى مكانه في الشريط العلوي ويصغر، والخلفية والاسم يتلاشيان،
        //    والرئيسي يصعد من خلفه: انتقال متصل بدل اختفاء مفاجئ
        var from = CenterInRoot(IntroLogo);
        var to = CenterInRoot(TopLogo);
        var scale = TopLogo.Bounds.Width / Math.Max(1, IntroLogo.Bounds.Width);
        _ = Animate(IntroLogo, t, 0, InOut,
            (TranslateTransform.XProperty, 0.0, to.X - from.X), (TranslateTransform.YProperty, 0.0, to.Y - from.Y),
            (ScaleTransform.ScaleXProperty, 1.0, scale), (ScaleTransform.ScaleYProperty, 1.0, scale));
        _ = Animate(IntroWords, t * 0.6, 0, Out, (OpacityProperty, 1.0, 0.0), (TranslateTransform.YProperty, 0.0, 30.0));
        _ = Animate(IntroLine, t * 0.5, 0, Out, (OpacityProperty, 1.0, 0.0), (ScaleTransform.ScaleXProperty, 1.0, 0.0));
        _ = Animate(IntroBg, t, 0.1, Out, (OpacityProperty, 1.0, 0.0));
        _ = Animate(IntroGlow, t, 0, Out, (OpacityProperty, 1.0, 0.0));
        _ = Animate(IntroGrid, t, 0, Out, (OpacityProperty, 1.0, 0.0));
        _ = MotionSettings.AnimateAsync(PageView, TimeSpan.FromSeconds(t + 0.2), Out, 0, 1.04, 24, delay: TimeSpan.FromSeconds(t * 0.25));
        _ = MotionSettings.AnimateAsync(TopBar, TimeSpan.FromSeconds(t), Out, 0, 1, -24, delay: TimeSpan.FromSeconds(t * 0.35));
        await Task.Delay(TimeSpan.FromSeconds(t));

        // وصل الشعار فوق زر الشعار الحقيقي: يذوب فيه
        _ = Animate(IntroLogo, 0.18, 0, Out, (OpacityProperty, 1.0, 0.0));
        await Task.Delay(180);
        Intro.IsVisible = false;
        IntroRunning = false;
        _introEnding = false;
        Stagger(RowItems, 45);
    }

    /// مركز العنصر بإحداثيات الجذر من التخطيط وحده (بلا تحويلات الحركة)
    private Point CenterInRoot(Visual v)
    {
        double x = v.Bounds.Width / 2, y = v.Bounds.Height / 2;
        for (Visual? p = v; p != null && p != Root; p = Avalonia.VisualTree.VisualExtensions.GetVisualParent(p))
        {
            x += p.Bounds.X;
            y += p.Bounds.Y;
        }
        return new Point(x, y);
    }

    /// دخول مرن كالهلام: يكبر أكثر من حجمه ثم يتمدد وينكمش حتى يستقر
    private static void ElasticIn(Visual v)
    {
        EnsureTransforms(v);
        (double Cue, double Sx, double Sy, double O)[] f =
        {
            (0, 0.25, 0.25, 0), (0.22, 1.2, 1.2, 1), (0.36, 1.12, 0.86, 1), (0.5, 0.9, 1.1, 1),
            (0.64, 1.06, 0.95, 1), (0.78, 0.97, 1.03, 1), (0.9, 1.01, 0.99, 1), (1, 1, 1, 1),
        };
        var anim = new Animation { Duration = TimeSpan.FromSeconds(1.15), Easing = new CubicEaseOut(), FillMode = FillMode.Both };
        foreach (var k in f)
            anim.Children.Add(new KeyFrame
            {
                Cue = new Cue(k.Cue),
                Setters =
                {
                    new Setter(ScaleTransform.ScaleXProperty, k.Sx),
                    new Setter(ScaleTransform.ScaleYProperty, k.Sy),
                    new Setter(OpacityProperty, k.O),
                },
            });
        _ = RunSafe(anim, v);
    }

    private static async Task RunSafe(Animation anim, Visual v)
    {
        try { await anim.RunAsync(v); }
        catch (Exception ex) { Log.Error("intro animation failed", ex); }
    }

    /// حركة من قيم بداية إلى نهاية؛ إن تكررت خاصية فالأولى تصل منتصف الحركة والثانية تكملها (ظهور ثم اختفاء)
    private static async Task Animate(Visual v, double seconds, double delay, Easing easing, params (AvaloniaProperty P, double From, double To)[] props)
    {
        try
        {
            EnsureTransforms(v);
            var start = new KeyFrame { Cue = new Cue(0) };
            var mid = new KeyFrame { Cue = new Cue(0.5) };
            var end = new KeyFrame { Cue = new Cue(1) };
            var firsts = new System.Collections.Generic.Dictionary<AvaloniaProperty, double>();
            foreach (var (p, from, to) in props)
            {
                if (!firsts.ContainsKey(p))
                {
                    firsts[p] = to;
                    start.Setters.Add(new Setter(p, from));
                }
                else
                {
                    mid.Setters.Add(new Setter(p, from));
                    firsts[p] = to;
                }
            }
            foreach (var (p, to) in firsts) end.Setters.Add(new Setter(p, to));

            var anim = new Animation
            {
                Duration = TimeSpan.FromSeconds(seconds),
                Delay = TimeSpan.FromSeconds(delay),
                Easing = easing,
                FillMode = FillMode.Both,
            };
            anim.Children.Add(start);
            if (mid.Setters.Count > 0) anim.Children.Add(mid);
            anim.Children.Add(end);
            await anim.RunAsync(v);
        }
        catch (Exception ex) { Log.Error("intro animation failed", ex); }
    }

    /// المقياس والإزاحة يحتاجان تحويلات موجودة على العنصر (الحركة تعمل على العنصر نفسه وتجدها)
    private static void EnsureTransforms(Visual v)
    {
        if (v.RenderTransform is TransformGroup) return;
        v.RenderTransform = new TransformGroup { Children = { new ScaleTransform(1, 1), new TranslateTransform(0, 0) } };
    }
}
