using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using HalawaHub.App.Services;

namespace HalawaHub.App.Controls;

public enum PillMotion { Spring, Dialog, Underline, Even }

/// <summary>
/// المؤشر المنزلق خلف العناصر المتساوية: طرفاه يتحركان بتوقيتين مختلفين فيتمدد ثم
/// ينكمش (نفس inset-inline-start/end في التصميم). يُرتب بقيم منطقية، والاتجاه
/// العربي يعكس الرسم تلقائيًا عبر FlowDirection.
/// </summary>
public class SlidingPill : Decorator
{
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<SlidingPill, int>(nameof(SelectedIndex));
    public static readonly StyledProperty<int> ItemCountProperty =
        AvaloniaProperty.Register<SlidingPill, int>(nameof(ItemCount), 1);
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<SlidingPill, Orientation>(nameof(Orientation), Orientation.Horizontal);
    /// بُعد الحاوية الداخلي (padding)
    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<SlidingPill, double>(nameof(Inset));
    /// فراغ إضافي داخل كل عنصر (خط التبويب أقصر من التبويب بـ 14 من كل جهة)
    public static readonly StyledProperty<double> EdgeInsetProperty =
        AvaloniaProperty.Register<SlidingPill, double>(nameof(EdgeInset));
    /// المسافة بين بدايات العناصر. 0 = تقسيم المساحة بالتساوي
    public static readonly StyledProperty<double> PitchProperty =
        AvaloniaProperty.Register<SlidingPill, double>(nameof(Pitch));
    public static readonly StyledProperty<PillMotion> MotionProperty =
        AvaloniaProperty.Register<SlidingPill, PillMotion>(nameof(Motion));

    private static readonly StyledProperty<double> StartEdgeProperty =
        AvaloniaProperty.Register<SlidingPill, double>("StartEdge");
    private static readonly StyledProperty<double> EndEdgeProperty =
        AvaloniaProperty.Register<SlidingPill, double>("EndEdge");

    static SlidingPill()
    {
        AffectsArrange<SlidingPill>(StartEdgeProperty, EndEdgeProperty);
    }

    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public int ItemCount { get => GetValue(ItemCountProperty); set => SetValue(ItemCountProperty, value); }
    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }
    public double EdgeInset { get => GetValue(EdgeInsetProperty); set => SetValue(EdgeInsetProperty, value); }
    public double Pitch { get => GetValue(PitchProperty); set => SetValue(PitchProperty, value); }
    public PillMotion Motion { get => GetValue(MotionProperty); set => SetValue(MotionProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedIndexProperty)
            MoveEdges(animate: true, forward: change.GetNewValue<int>() > change.GetOldValue<int>());
        else if (change.Property == ItemCountProperty || change.Property == BoundsProperty)
            MoveEdges(animate: false, forward: true);
    }

    private void MoveEdges(bool animate, bool forward)
    {
        var extent = Orientation == Orientation.Horizontal ? Bounds.Width : Bounds.Height;
        var count = Math.Max(1, ItemCount);
        var index = Math.Clamp(SelectedIndex, 0, count - 1);

        if (extent <= 0) return;

        var pitch = Pitch > 0 ? Pitch : (extent - 2 * Inset) / count;
        var start = Inset + index * pitch + EdgeInset;
        var end = extent - Inset - (count - 1 - index) * pitch - EdgeInset;

        Transitions = animate && MotionSettings.Enabled ? BuildTransitions(forward) : null;
        SetValue(StartEdgeProperty, start);
        SetValue(EndEdgeProperty, end);
    }

    private Transitions BuildTransitions(bool forward)
    {
        // الطرف الأمامي في اتجاه الحركة يبدأ أولًا، والخلفي يلحقه
        var (lead, trail) = Motion switch
        {
            PillMotion.Underline => (
                Edge(0.28, new CssBezierEasing(0.65, 0, 0.35, 1)),
                Edge(0.30, new CssBezierEasing(0.65, 0, 0.35, 1), 0.12)),
            PillMotion.Dialog => (
                Edge(0.40, new CssBezierEasing(0.3, 1.6, 0.5, 1)),
                Edge(0.60, new CssBezierEasing(0.3, 1.22, 0.5, 1))),
            PillMotion.Even => (
                Edge(0.42, new CssBezierEasing(0.3, 1.5, 0.5, 1)),
                Edge(0.42, new CssBezierEasing(0.3, 1.5, 0.5, 1))),
            _ => (
                Edge(0.42, new CssBezierEasing(0.3, 1.6, 0.5, 1)),
                Edge(0.62, new CssBezierEasing(0.3, 1.22, 0.5, 1))),
        };

        lead.Property = forward ? EndEdgeProperty : StartEdgeProperty;
        trail.Property = forward ? StartEdgeProperty : EndEdgeProperty;
        return new Transitions { lead, trail };
    }

    private static DoubleTransition Edge(double seconds, Easing easing, double delay = 0) => new()
    {
        Duration = TimeSpan.FromSeconds(seconds),
        Delay = TimeSpan.FromSeconds(delay),
        Easing = easing
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        Child?.Measure(availableSize);
        return default; // لا يؤثر في حجم الحاوية
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var start = GetValue(StartEdgeProperty);
        var length = Math.Max(0, GetValue(EndEdgeProperty) - start);

        var rect = Orientation == Orientation.Horizontal
            ? new Rect(start, 0, length, finalSize.Height)
            : new Rect(0, start, finalSize.Width, length);

        Child?.Arrange(rect);
        return finalSize;
    }
}
