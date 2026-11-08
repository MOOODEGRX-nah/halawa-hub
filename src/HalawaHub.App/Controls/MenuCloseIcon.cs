using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using HalawaHub.App.Services;

namespace HalawaHub.App.Controls;

/// <summary>
/// زر القائمة في البطاقة: ثلاثة خطوط (≡) تتحول إلى ✕ مع دوران 180 درجة (.mt في التصميم).
/// </summary>
public class MenuCloseIcon : Control
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<MenuCloseIcon, bool>(nameof(IsOpen));
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MenuCloseIcon, IBrush?>(nameof(Foreground), Brushes.White);

    private static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<MenuCloseIcon, double>("Progress");

    static MenuCloseIcon()
    {
        AffectsRender<MenuCloseIcon>(ProgressProperty, ForegroundProperty);
    }

    public MenuCloseIcon()
    {
        FlowDirection = FlowDirection.LeftToRight;

        if (MotionSettings.Enabled)
        {
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = ProgressProperty,
                    Duration = TimeSpan.FromSeconds(0.5),
                    Easing = new SplineEasing(0.34, 1.56, 0.64, 1)
                }
            };
        }
    }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
            SetValue(ProgressProperty, change.GetNewValue<bool>() ? 1d : 0d);
    }

    protected override Size MeasureOverride(Size availableSize) => new(20, 20);

    public override void Render(DrawingContext context)
    {
        var p = GetValue(ProgressProperty);
        var brush = Foreground ?? Brushes.White;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);

        const double halfLength = 9; // left:1px; right:1px
        const double thickness = 2.6;

        void Line(double yOffset, double angleDegrees, double scaleX, double alpha)
        {
            if (alpha <= 0) return;

            var matrix =
                Matrix.CreateScale(scaleX, 1) *
                Matrix.CreateTranslation(0, yOffset) *
                Matrix.CreateRotation(Math.PI * angleDegrees / 180) *
                Matrix.CreateRotation(Math.PI * p) *
                Matrix.CreateTranslation(center.X, center.Y);

            using (context.PushTransform(matrix))
            using (context.PushOpacity(Math.Clamp(alpha, 0, 1)))
            {
                context.DrawRectangle(brush, null,
                    new Rect(-halfLength, -thickness / 2, halfLength * 2, thickness), 2, 2);
            }
        }

        var scale = 1 + 0.08 * p;
        Line(-6 * (1 - p), 45 * p, scale, 1);
        Line(0, 0, 1 - 0.7 * p, 1 - p * 2);
        Line(6 * (1 - p), -45 * p, scale, 1);
    }
}
