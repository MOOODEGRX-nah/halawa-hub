using System;
using Avalonia;
using Avalonia.Controls;

namespace HalawaHub.App.Controls;

/// <summary>
/// يأخذ العرض المتاح حتى حد أقصى (Cap) ويبقى في بداية السطر، مثل max-width في CSS.
/// MaxWidth وحده في Avalonia يضع العنصر في المنتصف عندما تكون المساحة أكبر، فكانت صفوف
/// العنوان والمفتاح تطفو وسط النافذة الكبيرة. البداية يمين في العربي ويسار في الإنجليزي تلقائيًا.
/// </summary>
public class CappedWidth : Decorator
{
    public static readonly StyledProperty<double> CapProperty =
        AvaloniaProperty.Register<CappedWidth, double>(nameof(Cap), double.PositiveInfinity);

    static CappedWidth()
    {
        AffectsMeasure<CappedWidth>(CapProperty);
    }

    public double Cap { get => GetValue(CapProperty); set => SetValue(CapProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child == null) return default;

        var width = Math.Min(availableSize.Width, Cap);
        Child.Measure(availableSize.WithWidth(width));
        // في قياس بلا حد (StackPanel أفقي مثلًا) يكفي عرض المحتوى نفسه
        return double.IsInfinity(width) ? Child.DesiredSize : Child.DesiredSize.WithWidth(width);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, Math.Min(finalSize.Width, Cap), finalSize.Height));
        return finalSize;
    }
}
