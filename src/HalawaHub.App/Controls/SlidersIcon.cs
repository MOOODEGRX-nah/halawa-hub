using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using HalawaHub.App.Services;

namespace HalawaHub.App.Controls;

/// <summary>
/// أيقونة الإعدادات: ثلاثة خطوط عليها مقابض تنزلق للجهة المقابلة عند hover
/// (.sl في التصميم). Small هي النسخة الصغيرة في قائمة البطاقة.
/// </summary>
public class SlidersIcon : Control
{
    public static readonly StyledProperty<bool> IsFlippedProperty =
        AvaloniaProperty.Register<SlidersIcon, bool>(nameof(IsFlipped));
    public static readonly StyledProperty<bool> SmallProperty =
        AvaloniaProperty.Register<SlidersIcon, bool>(nameof(Small));
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<SlidersIcon, IBrush?>(nameof(Foreground), Brushes.White);
    public static readonly StyledProperty<IBrush?> RingProperty =
        AvaloniaProperty.Register<SlidersIcon, IBrush?>(nameof(Ring));

    private static readonly StyledProperty<double> Knob1Property =
        AvaloniaProperty.Register<SlidersIcon, double>("Knob1", 1);
    private static readonly StyledProperty<double> Knob2Property =
        AvaloniaProperty.Register<SlidersIcon, double>("Knob2", -1);
    private static readonly StyledProperty<double> Knob3Property =
        AvaloniaProperty.Register<SlidersIcon, double>("Knob3", 1);

    static SlidersIcon()
    {
        AffectsRender<SlidersIcon>(Knob1Property, Knob2Property, Knob3Property, ForegroundProperty, RingProperty);
        AffectsMeasure<SlidersIcon>(SmallProperty);
    }

    public SlidersIcon()
    {
        // الأيقونة لا تنعكس في الواجهة العربية، مثل SVG في التصميم
        FlowDirection = FlowDirection.LeftToRight;

        if (MotionSettings.Enabled)
        {
            Transitions = new Transitions
            {
                Knob(Knob1Property, 0),
                Knob(Knob2Property, 0.04),
                Knob(Knob3Property, 0.08),
            };
        }
    }

    private static DoubleTransition Knob(StyledProperty<double> property, double delay) => new()
    {
        Property = property,
        Duration = TimeSpan.FromSeconds(0.55),
        Delay = TimeSpan.FromSeconds(delay),
        Easing = new CssBezierEasing(0.34, 1.35, 0.5, 1)
    };

    internal double FirstKnob => GetValue(Knob1Property);

    public bool IsFlipped { get => GetValue(IsFlippedProperty); set => SetValue(IsFlippedProperty, value); }
    public bool Small { get => GetValue(SmallProperty); set => SetValue(SmallProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? Ring { get => GetValue(RingProperty); set => SetValue(RingProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsFlippedProperty)
        {
            var sign = change.GetNewValue<bool>() ? -1 : 1;
            SetValue(Knob1Property, (double)sign);
            SetValue(Knob2Property, (double)-sign);
            SetValue(Knob3Property, (double)sign);
        }
    }

    protected override Size MeasureOverride(Size availableSize) =>
        Small ? new Size(14, 11.4) : new Size(22, 17);

    public override void Render(DrawingContext context)
    {
        var (width, gap, line, knob, o1, o3, ring) = Small
            ? (14d, 4d, 1.1d, 3.4d, 3d, 1.8d, 0.8d)
            : (22d, 6d, 1.7d, 5d, 4.9d, 2.9d, 1d);

        var midX = Bounds.Width / 2;
        var midY = Bounds.Height / 2;
        var brush = Foreground ?? Brushes.White;
        var ringPen = Ring != null ? new Pen(Ring, ring * 2) : null;

        void Draw(double yOffset, double knobOffset)
        {
            var y = midY + yOffset;
            context.DrawRectangle(brush, null,
                new Rect(midX - width / 2 + 1, y - line / 2, width - 2, line), line / 2, line / 2);
            context.DrawEllipse(brush, ringPen, new Point(midX + knobOffset, y), knob / 2 + (ringPen != null ? ring / 2 : 0), knob / 2 + (ringPen != null ? ring / 2 : 0));
        }

        Draw(-gap, GetValue(Knob1Property) * o1);
        Draw(0, GetValue(Knob2Property) * o1);
        Draw(gap, GetValue(Knob3Property) * o3);
    }
}
