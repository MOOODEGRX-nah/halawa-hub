using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Styling;
using HalawaHub.App.Services;
using HalawaHub.Core;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// بداية الشاشة الكبيرة (مثل بداية Big Picture في Steam): الشعار يظهر بتوهج، ثم الاسم،
/// وموجة تعبر نقاط الشبكة من الشعار، وخط يمتلئ، ثم تتلاشى إلى الرئيسي. نحو ثانيتين،
/// وأي زر يتخطاها. "تقليل الحركة" يلغيها.
/// </summary>
public partial class BigPictureWindow
{
    private CancellationTokenSource? _introCts;
    internal bool IntroRunning { get; private set; }
    /// مدة بقاء الشعار بعد الموجة (الاختبارات تطيلها لتصوّر البداية وتجرّب التخطي)
    internal static TimeSpan IntroHold { get; set; } = TimeSpan.FromMilliseconds(1250);

    private async void PlayIntro()
    {
        if (!MotionSettings.Enabled) { Intro.IsVisible = false; return; }

        IntroRunning = true;
        Intro.IsVisible = true;
        Intro.Opacity = 1;
        var cts = _introCts = new CancellationTokenSource();
        try
        {
            IntroLogo.Opacity = 1;
            IntroText.Opacity = 1;
            IntroGlow.Opacity = 1;
            IntroGrid.Opacity = 1;
            // الضوء خلف الشعار مباشرة، بلا انزلاق
            IntroGrid.Transitions = null;
            IntroGrid.LightX = Root.Bounds.Width / 2;
            IntroGrid.LightY = 440;
            var bar = new ScaleTransform(0, 1);
            IntroBar.RenderTransform = bar;

            _ = FadeIn(IntroGlow, 0.9);
            _ = FadeIn(IntroGrid, 0.6);
            _ = MotionSettings.AnimateAsync(IntroLogo, TimeSpan.FromSeconds(0.7), BpSprings.Lift, 0, 0.6, 0);
            _ = MotionSettings.AnimateAsync(IntroText, TimeSpan.FromSeconds(0.55), BpSprings.Glide, 0, 1, 16, delay: TimeSpan.FromSeconds(0.3));
            _ = Run(IntroBar, 0.9, 0.35, (ScaleTransform.ScaleXProperty, 0.0, 1.0));

            await Task.Delay(400, cts.Token);
            IntroGrid.Wave(Root.Bounds.Width / 2, 440);
            await Task.Delay(IntroHold, cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Error("intro failed", ex); }
        await EndIntro();
    }

    /// أي زر أثناء البداية يتخطاها ولا يُنفَّذ
    private void SkipIntro() => _introCts?.Cancel();

    private bool _introEnding;

    private async Task EndIntro()
    {
        if (!IntroRunning || _introEnding) return;
        _introEnding = true;
        // الأزرار تبقى مبتلعة حتى ينتهي التلاشي، فلا يتحرك شيء خلف البداية
        Intro.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromSeconds(0.45), Easing = BpSprings.Glide } };
        Intro.Opacity = 0;
        await Task.Delay(450);
        Intro.IsVisible = false;
        Intro.Transitions = null;
        Intro.Opacity = 1;
        IntroRunning = false;
        _introEnding = false;
        // الواجهة تدخل كما عند تبديل الألعاب
        AnimateHero();
        Stagger(RowItems, 45);
    }

    private static Task FadeIn(Visual v, double seconds) => Run(v, seconds, 0, (OpacityProperty, 0.0, 1.0));

    private static async Task Run(Visual v, double seconds, double delay, (AvaloniaProperty P, double From, double To) prop)
    {
        var anim = new Animation
        {
            Duration = TimeSpan.FromSeconds(seconds),
            Delay = TimeSpan.FromSeconds(delay),
            Easing = BpSprings.Glide,
            FillMode = FillMode.Both,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(prop.P, prop.From) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(prop.P, prop.To) } },
            },
        };
        await anim.RunAsync(v);
    }
}
