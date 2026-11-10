using System;
using Avalonia;
using Avalonia.Controls;

namespace HalawaHub.App.Controls;

/// <summary>
/// يحدد حجم النافذة العائمة من حجم البرنامج: نسبة منه (80% افتراضيًا)، لا تصغر عن حجم
/// التصميم (MinFrameWidth/Height) ولا تتجاوز حدًا أقصى حتى لا يتمدد النص على الشاشات الكبيرة.
/// وإذا صار البرنامج أصغر من ذلك تصغر هي أيضًا لتبقى كاملة داخله، والمحتوى يتمرر.
/// تُوضع في المنتصف، وتتبع تغيير حجم البرنامج تلقائيًا لأن الحساب جزء من التخطيط نفسه.
/// </summary>
public class DialogFrame : Decorator
{
    public static readonly StyledProperty<double> WidthRatioProperty =
        AvaloniaProperty.Register<DialogFrame, double>(nameof(WidthRatio), 0.8);
    public static readonly StyledProperty<double> HeightRatioProperty =
        AvaloniaProperty.Register<DialogFrame, double>(nameof(HeightRatio), 0.8);
    public static readonly StyledProperty<double> MinFrameWidthProperty =
        AvaloniaProperty.Register<DialogFrame, double>(nameof(MinFrameWidth));
    public static readonly StyledProperty<double> MinFrameHeightProperty =
        AvaloniaProperty.Register<DialogFrame, double>(nameof(MinFrameHeight));
    public static readonly StyledProperty<double> MaxFrameWidthProperty =
        AvaloniaProperty.Register<DialogFrame, double>(nameof(MaxFrameWidth), 1400);
    public static readonly StyledProperty<double> MaxFrameHeightProperty =
        AvaloniaProperty.Register<DialogFrame, double>(nameof(MaxFrameHeight), 900);
    /// أقل مسافة بين النافذة وحواف البرنامج
    public static readonly StyledProperty<double> GutterProperty =
        AvaloniaProperty.Register<DialogFrame, double>(nameof(Gutter), 20);

    static DialogFrame()
    {
        AffectsMeasure<DialogFrame>(WidthRatioProperty, HeightRatioProperty, MinFrameWidthProperty, MinFrameHeightProperty,
            MaxFrameWidthProperty, MaxFrameHeightProperty, GutterProperty);
    }

    public double WidthRatio { get => GetValue(WidthRatioProperty); set => SetValue(WidthRatioProperty, value); }
    public double HeightRatio { get => GetValue(HeightRatioProperty); set => SetValue(HeightRatioProperty, value); }
    public double MinFrameWidth { get => GetValue(MinFrameWidthProperty); set => SetValue(MinFrameWidthProperty, value); }
    public double MinFrameHeight { get => GetValue(MinFrameHeightProperty); set => SetValue(MinFrameHeightProperty, value); }
    public double MaxFrameWidth { get => GetValue(MaxFrameWidthProperty); set => SetValue(MaxFrameWidthProperty, value); }
    public double MaxFrameHeight { get => GetValue(MaxFrameHeightProperty); set => SetValue(MaxFrameHeightProperty, value); }
    public double Gutter { get => GetValue(GutterProperty); set => SetValue(GutterProperty, value); }

    /// حجم النافذة لمساحة معينة. منفصل عن التخطيط ليُختبر وحده
    public static Size FrameSize(Size viewport, double widthRatio, double heightRatio,
        Size min, Size max, double gutter) => new(
        Fit(viewport.Width, widthRatio, min.Width, max.Width, gutter),
        Fit(viewport.Height, heightRatio, min.Height, max.Height, gutter));

    private static double Fit(double length, double ratio, double min, double max, double gutter)
    {
        // بدون مساحة محددة (قياس لا نهائي) يبقى حجم التصميم
        if (double.IsInfinity(length) || double.IsNaN(length)) return min;

        var wanted = Math.Clamp(length * ratio, min, Math.Max(min, max));
        var room = Math.Max(0, length - 2 * gutter);
        return Math.Floor(Math.Min(wanted, room));
    }

    private Size FrameSizeFor(Size viewport) => FrameSize(viewport, WidthRatio, HeightRatio,
        new Size(MinFrameWidth, MinFrameHeight), new Size(MaxFrameWidth, MaxFrameHeight), Gutter);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = FrameSizeFor(availableSize);
        Child?.Measure(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = FrameSizeFor(finalSize);
        Child?.Arrange(new Rect(
            Math.Floor((finalSize.Width - size.Width) / 2),
            Math.Floor((finalSize.Height - size.Height) / 2),
            size.Width, size.Height));
        return finalSize;
    }
}
