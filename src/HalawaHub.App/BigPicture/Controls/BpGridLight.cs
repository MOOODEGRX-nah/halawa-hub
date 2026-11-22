using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture.Controls;

/// <summary>
/// ضوء الشبكة (.gl0–.gl4 في التصميم، من grid-light.html): شبكة خافتة في كل الصفحة، وضوء
/// ينزلق إلى العنصر المركَّز فيضيء خطوط الشبكة ونقاطها حوله، وموجة تعبر النقاط عند A.
/// لا يتحرك إلا حين يتحرك التركيز: لا مؤقت ولا حركة دائمة وهو ساكن.
/// </summary>
public sealed class BpGridLight : Control
{
    private const double Cell = 40;

    public static readonly StyledProperty<double> LightXProperty =
        AvaloniaProperty.Register<BpGridLight, double>(nameof(LightX), 300);
    public static readonly StyledProperty<double> LightYProperty =
        AvaloniaProperty.Register<BpGridLight, double>(nameof(LightY), 760);
    public static readonly StyledProperty<double> WaveXProperty =
        AvaloniaProperty.Register<BpGridLight, double>(nameof(WaveX));
    public static readonly StyledProperty<double> WaveYProperty =
        AvaloniaProperty.Register<BpGridLight, double>(nameof(WaveY));
    public static readonly StyledProperty<double> WaveRadiusProperty =
        AvaloniaProperty.Register<BpGridLight, double>(nameof(WaveRadius));
    public static readonly StyledProperty<double> WaveOpacityProperty =
        AvaloniaProperty.Register<BpGridLight, double>(nameof(WaveOpacity));
    /// نصف قطر الظهور من الضوء (بداية الشاشة الكبيرة ترسم الشبكة من المركز للخارج). سالب = كامل
    public static readonly StyledProperty<double> RevealProperty =
        AvaloniaProperty.Register<BpGridLight, double>(nameof(Reveal), -1);

    static BpGridLight()
    {
        AffectsRender<BpGridLight>(LightXProperty, LightYProperty, WaveRadiusProperty, WaveOpacityProperty, RevealProperty);
    }

    public BpGridLight()
    {
        IsHitTestVisible = false;
        // يرث اتجاه الصفحة: الموضع بإحداثيات الجذر نفسها التي تستعملها حلقة التركيز
        ApplyMotion();
    }

    public double LightX { get => GetValue(LightXProperty); set => SetValue(LightXProperty, value); }
    public double LightY { get => GetValue(LightYProperty); set => SetValue(LightYProperty, value); }
    public double WaveX { get => GetValue(WaveXProperty); set => SetValue(WaveXProperty, value); }
    public double WaveY { get => GetValue(WaveYProperty); set => SetValue(WaveYProperty, value); }
    public double WaveRadius { get => GetValue(WaveRadiusProperty); set => SetValue(WaveRadiusProperty, value); }
    public double WaveOpacity { get => GetValue(WaveOpacityProperty); set => SetValue(WaveOpacityProperty, value); }
    public double Reveal { get => GetValue(RevealProperty); set => SetValue(RevealProperty, value); }

    /// cubic-bezier(.22,1.12,.36,1) في .95 ثانية، كما في --lx / --ly
    public void ApplyMotion()
    {
        Transitions = MotionSettings.Enabled
            ? new Transitions
            {
                new DoubleTransition { Property = LightXProperty, Duration = TimeSpan.FromSeconds(0.95), Easing = Glide },
                new DoubleTransition { Property = LightYProperty, Duration = TimeSpan.FromSeconds(0.95), Easing = Glide },
            }
            : null;
    }

    private static readonly CssBezierEasing Glide = new(0.22, 1.12, 0.36, 1);
    private static readonly CssBezierEasing WaveEase = new(0.22, 1, 0.36, 1);

    /// موجة عبر النقاط من مركز العنصر (ثانية واحدة)
    public async void Wave(double x, double y)
    {
        if (!MotionSettings.Enabled) return;
        WaveX = x;
        WaveY = y;
        var anim = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(1),
            Easing = WaveEase,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Avalonia.Styling.Setter(WaveRadiusProperty, 0.0), new Avalonia.Styling.Setter(WaveOpacityProperty, 1.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Avalonia.Styling.Setter(WaveRadiusProperty, 820.0), new Avalonia.Styling.Setter(WaveOpacityProperty, 0.0) } },
            },
        };
        try { await anim.RunAsync(this); }
        catch (Exception ex) { HalawaHub.Core.Log.Error("grid wave failed", ex); }
        WaveOpacity = 0;
    }

    private static readonly IPen Faint = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(11, 255, 210, 210)), 1);
    private static readonly IBrush Dot = new ImmutableSolidColorBrush(Color.FromArgb(242, 255, 220, 205));

    public override void Render(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var lx = LightX;
        var ly = LightY;
        var c = new Point(lx, ly);

        using var clip = Reveal >= 0 ? ctx.PushGeometryClip(new EllipseGeometry(new Rect(c.X - Reveal, c.Y - Reveal, Reveal * 2, Reveal * 2))) : default(DrawingContext.PushedState?);
        // .gl0: الشبكة الخافتة
        for (var x = 0.0; x <= w; x += Cell) ctx.DrawLine(Faint, new Point(x, 0), new Point(x, h));
        for (var y = 0.0; y <= h; y += Cell) ctx.DrawLine(Faint, new Point(0, y), new Point(w, y));

        // .gl1: الضوء نفسه (أحمر واسع، وكهرماني أصغر)
        ctx.DrawEllipse(Radial(c, 560, (0, Color.FromArgb(51, 239, 68, 68)), (0.45, Color.FromArgb(15, 239, 68, 68)), (0.7, Color.FromArgb(0, 239, 68, 68))), null, c, 560, 560);
        ctx.DrawEllipse(Radial(c, 220, (0, Color.FromArgb(36, 245, 166, 35)), (0.7, Color.FromArgb(0, 245, 166, 35))), null, c, 220, 220);

        // .gl2: خطوط الشبكة المضاءة حول الضوء
        var lit = new Pen(Radial(c, 400, (0, Color.FromArgb(128, 255, 170, 150)), (0.4, Color.FromArgb(45, 255, 170, 150)), (0.75, Color.FromArgb(0, 255, 170, 150))), 1.2);
        double x0 = Math.Max(0, Math.Floor((lx - 400) / Cell) * Cell), x1 = Math.Min(w, lx + 400);
        double y0 = Math.Max(0, Math.Floor((ly - 400) / Cell) * Cell), y1 = Math.Min(h, ly + 400);
        for (var x = x0; x <= x1; x += Cell) ctx.DrawLine(lit, new Point(x, Math.Max(0, ly - 400)), new Point(x, Math.Min(h, ly + 400)));
        for (var y = y0; y <= y1; y += Cell) ctx.DrawLine(lit, new Point(Math.Max(0, lx - 400), y), new Point(Math.Min(w, lx + 400), y));

        // .gl3: النقاط القريبة من الضوء (عند منتصفات الخلايا)
        DrawDots(ctx, w, h, lx, ly, 300, d => 1 - d / 300.0 * 1.25);

        // .gl4: الموجة: حلقة نقاط تتسع وتخفت
        var wo = WaveOpacity;
        if (wo > 0.01)
        {
            var wr = WaveRadius;
            DrawDots(ctx, w, h, WaveX, WaveY, wr + 34, d => wo * (1 - Math.Abs(d - wr) / 34.0));
        }
    }

    private static void DrawDots(DrawingContext ctx, double w, double h, double cx, double cy, double radius, Func<double, double> alpha)
    {
        var startX = Math.Max(Cell / 2, Math.Floor((cx - radius) / Cell) * Cell + Cell / 2);
        var startY = Math.Max(Cell / 2, Math.Floor((cy - radius) / Cell) * Cell + Cell / 2);
        for (var x = startX; x <= Math.Min(w, cx + radius); x += Cell)
        for (var y = startY; y <= Math.Min(h, cy + radius); y += Cell)
        {
            var d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (d > radius) continue;
            var a = alpha(d);
            if (a <= 0.02) continue;
            using (ctx.PushOpacity(Math.Min(1, a)))
                ctx.DrawEllipse(Dot, null, new Point(x, y), 1.9, 1.9);
        }
    }

    private static IBrush Radial(Point c, double r, params (double Offset, Color Color)[] stops)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(c, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(c, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(r, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(r, RelativeUnit.Absolute),
        };
        foreach (var (o, col) in stops) brush.GradientStops.Add(new GradientStop(col, o));
        return brush;
    }
}
