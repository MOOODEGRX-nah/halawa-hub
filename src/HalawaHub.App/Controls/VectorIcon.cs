using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace HalawaHub.App.Controls;

/// <summary>
/// أيقونة SVG بشبكة 24×24 مثل ملفات التصميم. لا تنعكس في الواجهة العربية،
/// فالسهم والمثلث يبقيان كما رسمهما التصميم.
/// </summary>
public class VectorIcon : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<VectorIcon, Geometry?>(nameof(Data));
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<VectorIcon, double>(nameof(Size), 18);
    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<VectorIcon, double>(nameof(StrokeWidth), 2);
    public static readonly StyledProperty<bool> IsFilledProperty =
        AvaloniaProperty.Register<VectorIcon, bool>(nameof(IsFilled));
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<VectorIcon>();
    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<VectorIcon, IBrush?>(nameof(FillBrush));
    public static readonly StyledProperty<IBrush?> StrokeBrushProperty =
        AvaloniaProperty.Register<VectorIcon, IBrush?>(nameof(StrokeBrush));

    static VectorIcon()
    {
        AffectsRender<VectorIcon>(DataProperty, StrokeWidthProperty, IsFilledProperty,
            ForegroundProperty, FillBrushProperty, StrokeBrushProperty);
        AffectsMeasure<VectorIcon>(SizeProperty);
    }

    public VectorIcon()
    {
        FlowDirection = FlowDirection.LeftToRight;
        RenderTransformOrigin = RelativePoint.Center;
    }

    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public double StrokeWidth { get => GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }
    public bool IsFilled { get => GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? StrokeBrush { get => GetValue(StrokeBrushProperty); set => SetValue(StrokeBrushProperty, value); }

    protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        if (Data == null) return;

        var scale = Size / 24d;
        var fill = IsFilled ? FillBrush ?? Foreground : FillBrush;
        var pen = IsFilled && StrokeBrush == null
            ? null
            : new Pen(StrokeBrush ?? Foreground, StrokeWidth, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            context.DrawGeometry(fill, pen, Data);
    }
}
