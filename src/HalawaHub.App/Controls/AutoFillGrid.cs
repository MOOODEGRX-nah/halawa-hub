using System;
using Avalonia;
using Avalonia.Controls;

namespace HalawaHub.App.Controls;

/// <summary>
/// شبكة البطاقات: grid-template-columns: repeat(auto-fill, minmax(170px, 1fr))
/// بفراغ 20 ونسبة 2:3. عدد الأعمدة يتغير مع عرض النافذة، وأقل عرض للبطاقة
/// (MinCell) يأتي من إعداد "حجم البطاقة": صغير 140، مريح 170، كبير 220.
/// </summary>
public class AutoFillGrid : Panel
{
    public const double MinCellWidth = 170;
    public const double Gap = 20;
    private const double AspectRatio = 1.5;

    public static readonly StyledProperty<double> MinCellProperty =
        AvaloniaProperty.Register<AutoFillGrid, double>(nameof(MinCell), MinCellWidth);

    static AutoFillGrid()
    {
        AffectsMeasure<AutoFillGrid>(MinCellProperty);
    }

    public double MinCell
    {
        get => GetValue(MinCellProperty);
        set => SetValue(MinCellProperty, value);
    }

    internal static int ColumnsFor(double width, double minCell = MinCellWidth) =>
        Math.Max(1, (int)Math.Floor((width + Gap) / (minCell + Gap)));

    private (int Columns, double CellWidth, double CellHeight) Layout(double width)
    {
        var min = MinCell > 40 ? MinCell : MinCellWidth;
        var columns = ColumnsFor(width, min);
        var cell = Math.Max(min, (width - Gap * (columns - 1)) / columns);
        return (columns, cell, cell * AspectRatio);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1224 : availableSize.Width;
        var (columns, cellW, cellH) = Layout(width);

        foreach (var child in Children)
            child.Measure(new Size(cellW, cellH));

        var rows = (int)Math.Ceiling(Children.Count / (double)columns);
        var height = rows == 0 ? 0 : rows * cellH + (rows - 1) * Gap;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, cellW, cellH) = Layout(finalSize.Width);

        for (var i = 0; i < Children.Count; i++)
        {
            var col = i % columns;
            var row = i / columns;
            Children[i].Arrange(new Rect(col * (cellW + Gap), row * (cellH + Gap), cellW, cellH));
        }

        return finalSize;
    }
}
