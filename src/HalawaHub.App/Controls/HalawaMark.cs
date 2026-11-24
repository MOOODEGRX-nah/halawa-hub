using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HalawaHub.App.Services;

namespace HalawaHub.App.Controls;

/// <summary>
/// شعار Halawa العائم (حرف M ثلاثي الأبعاد) كما في التصميم (.logo / .fm / .fms في Screen.dc.html):
/// نسخة أغمق تحته للعمق، وجه بتدرّج #FF9A85 → #EF4444 → #9F1A1A، حافة فاتحة، توهج أحمر،
/// وظل صغير تحته. عند المرور يرتفع 3 بكسل ويقوى التوهج ويصغر الظل.
/// رسم متجه واحد مشترك بين النافذة العادية والإعدادات والشاشة الكبيرة.
/// </summary>
public class HalawaMark : Control
{
    /// مسار الحرف من التصميم، داخل viewBox = "100 60 740 880"
    public const string PathData =
        "M120 220 L290 80 L470 232 L650 80 L818 220 Q762 262 758 320 L758 560 Q758 615 795 652 L640 782 L640 215 " +
        "L618 198 L528 270 L528 770 Q528 822 572 840 L470 925 L368 840 Q412 822 410 770 L410 270 L320 198 L300 215 " +
        "L300 782 L145 652 Q182 615 182 560 L182 320 Q178 262 120 220 Z";

    private const double ViewX = 100, ViewY = 60, ViewW = 740, ViewH = 880;

    private static readonly Geometry Shape = StreamGeometry.Parse(PathData);

    private static readonly IBrush FaceBrush = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse("#FF9A85"), 0),
            new GradientStop(Color.Parse("#EF4444"), 0.4),
            new GradientStop(Color.Parse("#9F1A1A"), 1)
        }
    };

    private static readonly IBrush DepthBrush = new SolidColorBrush(Color.Parse("#5E0E0E"));
    private static readonly IPen RimPen = new Pen(new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xE1, 0xD7)), 12, lineJoin: PenLineJoin.Round);

    private static readonly IBrush ShadeBrush = new RadialGradientBrush
    {
        GradientStops = { new GradientStop(Color.FromArgb(0xCC, 0, 0, 0), 0), new GradientStop(Colors.Transparent, 1) }
    };

    /// الارتفاع الحالي (0 → 1): يتحرك بنابض عند المرور
    public static readonly StyledProperty<double> LiftProperty =
        AvaloniaProperty.Register<HalawaMark, double>(nameof(Lift));

    /// يرتفع عند مرور المؤشر (النافذة العادية)، أو يبقى ثابتًا (الشاشة الكبيرة)
    public static readonly StyledProperty<bool> LiftOnHoverProperty =
        AvaloniaProperty.Register<HalawaMark, bool>(nameof(LiftOnHover), true);

    /// إظهار الظل البيضاوي تحت الحرف
    public static readonly StyledProperty<bool> ShowShadeProperty =
        AvaloniaProperty.Register<HalawaMark, bool>(nameof(ShowShade), true);

    /// لمعة تعبر الحرف (0 → 1)، داخل شكله فقط؛ خارج هذا المدى لا تُرسم
    public static readonly StyledProperty<double> ShineProperty =
        AvaloniaProperty.Register<HalawaMark, double>(nameof(Shine), -1);

    /// قوة التوهج الأحمر حوله (1 = كما في الشريط العلوي)
    public static readonly StyledProperty<double> GlowProperty =
        AvaloniaProperty.Register<HalawaMark, double>(nameof(Glow), 1);

    public double Shine { get => GetValue(ShineProperty); set => SetValue(ShineProperty, value); }
    public double Glow { get => GetValue(GlowProperty); set => SetValue(GlowProperty, value); }
    public double Lift { get => GetValue(LiftProperty); set => SetValue(LiftProperty, value); }
    public bool LiftOnHover { get => GetValue(LiftOnHoverProperty); set => SetValue(LiftOnHoverProperty, value); }
    public bool ShowShade { get => GetValue(ShowShadeProperty); set => SetValue(ShowShadeProperty, value); }

    private readonly DropShadowEffect _glow = new() { OffsetX = 0, OffsetY = 0, BlurRadius = 10, Color = Color.FromArgb(0x80, 0xEF, 0x44, 0x44) };

    static HalawaMark()
    {
        AffectsRender<HalawaMark>(LiftProperty, ShowShadeProperty, ShineProperty);
    }

    public HalawaMark()
    {
        Effect = _glow;
        IsHitTestVisible = true;
        Transitions = new Transitions
        {
            // .35s cubic-bezier(.34,1.56,.64,1)
            new DoubleTransition { Property = LiftProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = Springs.Pop }
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GlowProperty)
        {
            _glow.BlurRadius = 10 * Glow;
            _glow.Color = Color.FromArgb((byte)Math.Clamp(0x80 * Math.Min(Glow, 1.8), 0, 255), 0xEF, 0x44, 0x44);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (!LiftOnHover) return;
        Lift = 1;
        _glow.BlurRadius = 14;
        _glow.Color = Color.FromArgb(0xBF, 0xEF, 0x44, 0x44);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Lift = 0;
        _glow.BlurRadius = 10;
        _glow.Color = Color.FromArgb(0x80, 0xEF, 0x44, 0x44);
    }

    public override void Render(DrawingContext context)
    {
        var b = Bounds;
        if (b.Width <= 0 || b.Height <= 0) return;

        // في التصميم: الحرف 34×40 داخل مربع 44×44، والظل 30×7 في الأسفل
        var shadeH = ShowShade ? b.Height * 7 / 44 : 0;
        var areaH = b.Height - shadeH * 0.6;
        var scale = Math.Min(b.Width * 34 / 44 / ViewW, areaH * 40 / 44 / ViewH);
        var w = ViewW * scale;
        var h = ViewH * scale;
        var x = (b.Width - w) / 2;
        var y = (areaH - h) / 2 - Lift * 3;

        if (ShowShade)
        {
            var sw = b.Width * 30 / 44 * (1 - 0.25 * Lift);
            var sh = shadeH * (1 - 0.25 * Lift);
            using (context.PushOpacity(1 - 0.4 * Lift))
                context.DrawEllipse(ShadeBrush, null, new Point(b.Width / 2, b.Height - shadeH / 2), sw / 2, sh / 2);
        }

        var m = Matrix.CreateTranslation(-ViewX, -ViewY) * Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y);
        using (context.PushTransform(Matrix.CreateTranslation(0, 26 * scale) * m))
            context.DrawGeometry(DepthBrush, null, Shape);
        using (context.PushTransform(m))
        {
            context.DrawGeometry(FaceBrush, null, Shape);
            context.DrawGeometry(null, RimPen, Shape);
            if (Shine is > 0 and < 1)
            {
                // شريط ضوء مائل يمر من اليسار لليمين داخل حدود الحرف
                using (context.PushGeometryClip(Shape))
                {
                    var x0 = ViewX - 260 + Shine * (ViewW + 520);
                    var band = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(Color.FromArgb(0, 255, 244, 236), 0), new GradientStop(Color.FromArgb(0xB3, 255, 244, 236), 0.5), new GradientStop(Color.FromArgb(0, 255, 244, 236), 1) },
                    };
                    using (context.PushTransform(Matrix.CreateSkew(-0.36, 0) * Matrix.CreateTranslation(x0, 0)))
                        context.FillRectangle(band, new Rect(0, ViewY - 100, 180, ViewH + 200));
                }
            }
        }
    }

}
