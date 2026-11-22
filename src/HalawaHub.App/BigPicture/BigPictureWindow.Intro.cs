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
            _ = Animate(IntroLogo, 0.8, 0, Spring, (OpacityProperty, 0.0, 1.0), (ScaleTransform.ScaleXProperty, 0.55, 1.0), (ScaleTransform.ScaleYProperty, 0.55, 1.0));
            _ = Animate(IntroHalo, 1.0, 0.15, Out, (OpacityProperty, 0.0, 1.0), (ScaleTransform.ScaleXProperty, 0.6, 1.0), (ScaleTransform.ScaleYProperty, 0.6, 1.0));
            IntroShine.RenderTransform = new TransformGroup { Children = { new RotateTransform(20), new TranslateTransform(-260, 0) } };
            _ = Animate(IntroShine, 0.75, 0.55, InOut, (TranslateTransform.XProperty, -260.0, 260.0));
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
        var seconds = skipped ? 0.3 : 0.6;

        // 5) عبور: الشعار والاسم يكبران ويتلاشيان، والرئيسي يدخل من خلفهما
        _ = Animate(IntroContent, seconds, 0, InOut, (ScaleTransform.ScaleXProperty, 1.0, 1.6), (ScaleTransform.ScaleYProperty, 1.0, 1.6));
        Intro.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromSeconds(seconds), Easing = Out } };
        Intro.Opacity = 0;
        _ = MotionSettings.AnimateAsync(PageView, TimeSpan.FromSeconds(seconds + 0.25), Out, 0, 1.06, 0);
        _ = MotionSettings.AnimateAsync(TopBar, TimeSpan.FromSeconds(seconds + 0.25), Out, 0, 1, -30, delay: TimeSpan.FromSeconds(0.1));
        await Task.Delay(TimeSpan.FromSeconds(seconds));

        Intro.IsVisible = false;
        Intro.Transitions = null;
        Intro.Opacity = 1;
        IntroRunning = false;
        _introEnding = false;
        Stagger(RowItems, 45, 120);
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
