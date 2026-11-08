using System;
using Avalonia;
using Avalonia.Controls;

namespace HalawaHub.App.Controls;

/// <summary>
/// شبكة البطاقات: grid-template-columns: repeat(auto-fill, minmax(170px, 1fr))
/// بفراغ 20 ونسبة 2:3. عدد الأعمدة يتغير مع عرض النافذة.
/// </summary>
public class AutoFillGrid : Panel
{
    public const double MinCellWidth = 170;
    public const double Gap = 20;
    private const double AspectRatio = 1.5;

    internal static int ColumnsFor(double width) =>
        Math.Max(1, (int)Math.Floor((width + Gap) / (MinCellWidth + Gap)));

    private static (int Columns, double CellWidth, double CellHeight) Layout(double width)
    {
        var columns = ColumnsFor(width);
        var cell = Math.Max(MinCellWidth, (width - Gap * (columns - 1)) / columns);
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
