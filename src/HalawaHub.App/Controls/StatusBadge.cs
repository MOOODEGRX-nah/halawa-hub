using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using HalawaHub.App.Services;
using Path = Avalonia.Controls.Shapes.Path;

namespace HalawaHub.App.Controls;

public enum BadgeState { None, Pending, Done, Error }

/// <summary>
/// شارة الحالة (.pdb في التصميم): حلقة متقطعة تدور (انتظار) ثم ✓ تُرسم (نجاح)
/// أو ✗ مع اهتزاز (فشل). حجمها 32 بعد zoom:.8 داخل صندوق الحالة.
/// </summary>
public class StatusBadge : Panel
{
    public static readonly StyledProperty<BadgeState> StateProperty =
        AvaloniaProperty.Register<StatusBadge, BadgeState>(nameof(State), BadgeState.Pending);

    private const double Zoom = 0.8;

    private static readonly IBrush DoneFill = Gradient(("#991B1B", 0), ("#EF4444", 0.6), ("#F5A623", 1));
    private static readonly IBrush ErrorFill = Gradient(("#450A0A", 0), ("#B91C1C", 0.7), ("#DC2626", 1));

    private readonly Ellipse _ring;
    private readonly Ellipse _dot;
    private readonly Border _fill;
    private readonly Path _check;
    private readonly Path _cross;

    public StatusBadge()
    {
        Width = 40 * Zoom;
        Height = 40 * Zoom;
        FlowDirection = FlowDirection.LeftToRight;

        _ring = new Ellipse
        {
            Margin = new Thickness(4 * Zoom),
            Stroke = new SolidColorBrush(Color.Parse("#80DCBEBE")),
            StrokeThickness = 3.5 * Zoom,
            StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 1.6, 1.3 },
            RenderTransform = new RotateTransform(),
            RenderTransformOrigin = RelativePoint.Center,
        };

        _dot = new Ellipse
        {
            Width = 8 * Zoom,
            Height = 8 * Zoom,
            Fill = new SolidColorBrush(Color.Parse("#F5A623")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = new ScaleTransform(),
            RenderTransformOrigin = RelativePoint.Center,
        };

        _fill = new Border
        {
            Margin = new Thickness(4 * Zoom),
            CornerRadius = new CornerRadius(99),
            RenderTransformOrigin = RelativePoint.Center,
        };

        _check = Mark("M6 12.5l4 4 8-9", 6);
        _cross = Mark("M8 8l8 8M16 8l-8 8", 4);

        Children.Add(_ring);
        Children.Add(_dot);
        Children.Add(_fill);
        Children.Add(_check);
        Children.Add(_cross);

        Apply(animate: false);
    }

    public BadgeState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
            Apply(animate: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartPendingLoops();
    }

    private void Apply(bool animate)
    {
        var state = State;
        var pending = state == BadgeState.Pending;
        var done = state == BadgeState.Done;
        var error = state == BadgeState.Error;

        _ring.IsVisible = pending;
        _dot.IsVisible = pending;
        _fill.IsVisible = done || error;
        _check.IsVisible = done;
        _cross.IsVisible = error;

        _fill.Background = error ? ErrorFill : DoneFill;
        _fill.BoxShadow = error
            ? BoxShadows.Parse("0 0 11 0 #8CB91C1C")
            : BoxShadows.Parse("0 0 14 0 #A6EF4444");

        _check.StrokeDashOffset = 0;
        _cross.StrokeDashOffset = 0;

        if (pending)
        {
            StartPendingLoops();
            return;
        }

        if (!animate || !MotionSettings.Enabled) return;

        if (done)
        {
            _ = MotionSettings.AnimateAsync(_fill, TimeSpan.FromSeconds(0.5), new SplineEasing(0.34, 1.6, 0.64, 1), 0, 0.6, 0);
            _ = DrawAsync(_check, 6);
        }
        else if (error)
        {
            _ = MotionSettings.AnimateAsync(_fill, TimeSpan.FromSeconds(0.2), new LinearEasing(), 0, 0.6, 0);
            _ = ShakeAsync();
            _ = DrawAsync(_cross, 4);
        }
    }

    private void StartPendingLoops()
    {
        if (!MotionSettings.Enabled || !_ring.IsVisible || VisualRoot == null) return;

        new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(4),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(RotateTransform.AngleProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RotateTransform.AngleProperty, 360d) } },
            }
        }.RunAsync(_ring);

        new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(1),
            IterationCount = IterationCount.Infinite,
            Easing = new SineEaseInOut(),
            Children =
            {
                Breath(0, 0.35, 0.8),
                Breath(0.5, 0.9, 1.3),
                Breath(1, 0.35, 0.8),
            }
        }.RunAsync(_dot);
    }

    private static KeyFrame Breath(double cue, double opacity, double scale) => new()
    {
        Cue = new Cue(cue),
        Setters =
        {
            new Setter(OpacityProperty, opacity),
            new Setter(ScaleTransform.ScaleXProperty, scale),
            new Setter(ScaleTransform.ScaleYProperty, scale),
        }
    };

    private static async System.Threading.Tasks.Task DrawAsync(Shape mark, double length)
    {
        await new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Delay = TimeSpan.FromSeconds(0.12),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Backward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Shape.StrokeDashOffsetProperty, length) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Shape.StrokeDashOffsetProperty, 0d) } },
            }
        }.RunAsync(mark);
    }

    private async System.Threading.Tasks.Task ShakeAsync()
    {
        MotionSettings.PrepareTransform(_fill);

        await new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(0.32),
            Delay = TimeSpan.FromSeconds(0.18),
            Children =
            {
                Shake(0, 0), Shake(0.27, -3), Shake(0.53, 3), Shake(0.8, -1.5), Shake(1, 0),
            }
        }.RunAsync(_fill);
    }

    private static KeyFrame Shake(double cue, double x) => new()
    {
        Cue = new Cue(cue),
        Setters = { new Setter(TranslateTransform.XProperty, x) }
    };

    private static Path Mark(string data, double dash) => new()
    {
        Data = Geometry.Parse(data),
        Width = 24 * Zoom,
        Height = 24 * Zoom,
        Stretch = Stretch.None,
        Stroke = Brushes.White,
        StrokeThickness = 3,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { dash, dash },
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        RenderTransform = new ScaleTransform(Zoom, Zoom),
        RenderTransformOrigin = RelativePoint.TopLeft,
    };

    private static IBrush Gradient(params (string Color, double Offset)[] stops)
    {
        var stopsList = new List<ImmutableGradientStop>();
        foreach (var (color, offset) in stops)
            stopsList.Add(new ImmutableGradientStop(offset, Color.Parse(color)));

        return new Avalonia.Media.Immutable.ImmutableLinearGradientBrush(
            stopsList,
            startPoint: RelativePoint.TopLeft,
            endPoint: RelativePoint.BottomRight);
    }
}
