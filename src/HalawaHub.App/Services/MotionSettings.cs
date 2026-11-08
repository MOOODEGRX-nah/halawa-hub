using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace HalawaHub.App.Services;

/// <summary>
/// إعدادات الحركة المشتركة. يحترم خيار "إيقاف الحركات" في ويندوز.
/// Enabled=false يجعل كل عنصر يظهر مباشرة بحالته النهائية.
/// </summary>
public static class MotionSettings
{
    public static bool Enabled { get; set; } = ReadSystemSetting();

    /// cubic-bezier(.22,1.2,.36,1): دخول البطاقات والأقسام
    public static Easing EnterEasing { get; } = new SplineEasing(0.22, 1.2, 0.36, 1);

    /// cubic-bezier(.34,1.4,.64,1): دخول النوافذ
    public static Easing DialogEasing { get; } = new SplineEasing(0.34, 1.4, 0.64, 1);

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

        var animation = new Avalonia.Animation.Animation
        {
            Duration = duration,
            Delay = delay,
            Easing = easing,
            FillMode = FillMode.Both,
            Children =
            {
                Frame(0, fromOpacity, fromScale, fromY),
                Frame(1, toOpacity, toScale, toY),
            }
        };

        await animation.RunAsync(visual);
    }

    private static KeyFrame Frame(double cue, double opacity, double scale, double y) => new()
    {
        Cue = new Cue(cue),
        Setters =
        {
            new Setter(Visual.OpacityProperty, opacity),
            new Setter(ScaleTransform.ScaleXProperty, scale),
            new Setter(ScaleTransform.ScaleYProperty, scale),
            new Setter(TranslateTransform.YProperty, y),
        }
    };

    private static bool ReadSystemSetting()
    {
        if (!OperatingSystem.IsWindows()) return true;

        try
        {
            const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
            return !SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, out var enabled, 0) || enabled;
        }
        catch
        {
            return true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, [MarshalAs(UnmanagedType.Bool)] out bool value, uint winIni);
}
