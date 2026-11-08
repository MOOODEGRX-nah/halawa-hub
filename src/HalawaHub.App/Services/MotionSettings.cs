using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace HalawaHub.App.Services;

/// <summary>
/// إعدادات الحركة المشتركة. الحركة جزء من تصميم البرنامج فتعمل دائمًا، حتى لو كان
/// خيار "تأثيرات الحركة" معطلًا في ويندوز: ربطها بهذا الخيار كان يوقف كل الحركات
/// بصمت عند من عطّله لتسريع جهازه.
/// Enabled=false للاختبارات فقط: يجعل كل عنصر يظهر مباشرة بحالته النهائية.
/// </summary>
public static class MotionSettings
{
    public static bool Enabled { get; set; } = true;

    /// cubic-bezier(.22,1.2,.36,1): دخول البطاقات والأقسام
    public static Easing EnterEasing { get; } = new CssBezierEasing(0.22, 1.2, 0.36, 1);

    /// cubic-bezier(.34,1.4,.64,1): دخول النوافذ
    public static Easing DialogEasing { get; } = new CssBezierEasing(0.34, 1.4, 0.64, 1);

    /// <summary>
    /// تحويل صريح لكل عنصر متحرك: تحريك RenderTransform كنص في KeyFrame يكسر
    /// البرنامج في Avalonia (انظر إصدار 0.0.10.9)، فنحرّك ScaleTransform وTranslateTransform.
    /// </summary>
    public static void PrepareTransform(Visual visual)
    {
        if (visual.RenderTransform is TransformGroup group && group.Children.Count == 2) return;
        visual.RenderTransform = new TransformGroup
        {
            Children = { new ScaleTransform(1, 1), new TranslateTransform(0, 0) }
        };
    }

    public static async Task AnimateAsync(
        Animatable visual, TimeSpan duration, Easing easing,
        double fromOpacity, double fromScale, double fromY,
        double toOpacity = 1, double toScale = 1, double toY = 0,
        TimeSpan delay = default)
    {
        if (!Enabled || visual is not Visual target) return;

        PrepareTransform(target);

        // حركتان بدل واحدة: الشكل يرتد مع المنحنى، أما الشفافية فتبقى بين 0 و1
        var transform = new Avalonia.Animation.Animation
        {
            Duration = duration,
            Delay = delay,
            Easing = easing,
            FillMode = FillMode.Both,
            Children =
            {
                TransformFrame(0, fromScale, fromY),
                TransformFrame(1, toScale, toY),
            }
        };

        var opacity = new Avalonia.Animation.Animation
        {
            Duration = duration,
            Delay = delay,
            Easing = new ClampedEasing(easing),
            FillMode = FillMode.Both,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, fromOpacity) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, toOpacity) } },
            }
        };

        await Task.WhenAll(transform.RunAsync(visual), opacity.RunAsync(visual));
    }

    /// <summary>
    /// Loop until cancelled. IterationCount.Infinite with RunAsync returns a faulted Task
    /// (Looping animations must not use the Run method) that surfaces later in the log.
    /// </summary>
    public static async Task RunLoopAsync(Avalonia.Animation.Animation animation, Animatable target, System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
            await animation.RunAsync(target, token);
    }

    /// <summary>
    /// أول فتح لنافذة يبني كل عناصرها، والحركة مرتبطة بالوقت: لو بدأت قبل انتهاء
    /// البناء لضاع جزؤها الأول وبدت قفزة. نخفي العنصر حتى ينتهي البناء ثم نبدأ.
    /// </summary>
    public static async Task AnimateAfterLayoutAsync(
        Animatable visual, TimeSpan duration, Easing easing,
        double fromOpacity, double fromScale, double fromY)
    {
        if (!Enabled || visual is not Visual target) return;

        target.Opacity = fromOpacity;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        target.ClearValue(Visual.OpacityProperty);

        await AnimateAsync(visual, duration, easing, fromOpacity, fromScale, fromY);
    }

    private static KeyFrame TransformFrame(double cue, double scale, double y) => new()
    {
        Cue = new Cue(cue),
        Setters =
        {
            new Setter(ScaleTransform.ScaleXProperty, scale),
            new Setter(ScaleTransform.ScaleYProperty, scale),
            new Setter(TranslateTransform.YProperty, y),
        }
    };
}
