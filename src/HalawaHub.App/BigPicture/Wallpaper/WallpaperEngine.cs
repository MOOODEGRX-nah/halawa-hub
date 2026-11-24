using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace HalawaHub.App.BigPicture.Wallpaper;

/// ألوان المشهد لكل لون تمييز (WP_PAL في BigPicture.dc.html): a أساسي، b ثانوي، c ثالث، d فاتح، e داكن، h درجة اللون
public sealed record WallPalette(string A, string B, string C, string D, string E, double H)
{
    public static WallPalette For(string accent) => accent switch
    {
        "blue" => new("59,130,246", "56,198,255", "110,91,255", "200,225,255", "30,58,138", 222),
        "purple" => new("168,85,247", "255,91,184", "110,91,255", "235,215,255", "88,28,135", 275),
        "green" => new("22,163,74", "61,219,176", "56,198,255", "200,250,215", "20,83,45", 145),
        "orange" => new("234,88,12", "245,166,35", "239,68,68", "255,220,180", "124,45,18", 24),
        _ => new("239,68,68", "245,166,35", "190,24,93", "255,200,190", "127,29,29", 4),
    };
}

/// <summary>
/// الخلفيات المتحركة (تحديث التصميم 7): نقل مباشر لـ makeWall في BigPicture.dc.html والأصول في
/// reference/wallpapers. المشهد بإحداثيات 480×270 ويُرسم على سطح 960×540 (نصف دقة 1080p) ثم يُكبَّر.
/// النقرة (tap) مركز العنصر المركَّز. لا شيء هنا يعمل ما لم يطلب المضيف إطارًا.
/// </summary>
public sealed class WallpaperEngine
{
    public const float W = 480, H = 270, CX = W / 2, CY = H / 2;
    private const float Tau = MathF.PI * 2;

    public static readonly string[] Ids = { "grid", "blobs", "aurora", "grain", "dots", "flow", "contour", "stars", "spot", "plain" };

    internal readonly Random Rng;
    internal SKCanvas Canvas = null!;
    internal WallPalette P = WallPalette.For("red");
    internal SKColor Bg = new(10, 4, 4);
    internal SKBlendMode Blend = SKBlendMode.SrcOver;
    internal double Clock = 2000;            // ms
    internal double ClickAt = -1e9;          // ms
    internal (float X, float Y) Tap = (CX, CY);
    internal readonly List<(float X, float Y, double T)> Taps = new();

    private IScene? _scene;
    public string SceneId { get; private set; } = "";
    public bool React { get; set; } = true;

    public WallpaperEngine(int seed = 7) => Rng = new Random(seed);

    internal double Since(double now) => (now - ClickAt) / 1000;

    public void SetScene(string id)
    {
        if (id == SceneId) return;
        SceneId = id;
        _scene = id switch
        {
            "blobs" => new Blobs(this),
            "aurora" => new Aurora(this),
            "grain" => new Grain(this),
            "dots" => new Dots(this),
            "flow" => new Flow(this),
            "contour" => new Contour(this),
            "stars" => new Stars(this),
            "spot" => new Spot(this),
            _ => null,
        };
        _scene?.Init();
        Clock = 2000;
        ClickAt = -1e9;
        Taps.Clear();
    }

    public bool HasScene => _scene != null;

    public void SetColors(string accent, bool oled)
    {
        P = WallPalette.For(accent);
        Bg = oled ? new SKColor(0, 0, 0) : new SKColor(10, 4, 4);
    }

    /// نقرة عند مركز العنصر المركَّز (بإحداثيات المشهد)
    public void OnTap(float x, float y)
    {
        if (_scene == null || !React) return;
        _scene.OnTap((x, y), Clock);
        Tap = (x, y);
        ClickAt = Clock;
        Taps.Add((x, y, Clock));
        if (Taps.Count > 6) Taps.RemoveAt(0);
    }

    /// إطار واحد: يتقدم الوقت dt ثانية ثم يرسم على اللوحة (بحجمها الكامل؛ المشهد يُكبَّر ليملأها)
    public void Frame(SKCanvas canvas, int pixelWidth, int pixelHeight, double dt)
    {
        Canvas = canvas;
        Clock += dt * 1000;
        canvas.Save();
        canvas.Scale(pixelWidth / W, pixelHeight / H);
        Blend = SKBlendMode.SrcOver;
        if (_scene == null) canvas.Clear(Bg);
        else _scene.Draw(Clock, Clock / 1000, dt);
        canvas.Restore();
    }

    /// يتقدم بالوقت دون رسم كل خطوة (معاينة ثابتة تشبه ما بعد ثوانٍ من الحركة)
    public void Advance(double seconds)
    {
        if (_scene is Stars or Spot)
        {
            for (double t = 0; t < seconds; t += 1.0 / 30) { Clock += 1000.0 / 30; _scene.Step(Clock, 1.0 / 30); }
        }
        else Clock += seconds * 1000;
    }

    // ===== أدوات الرسم (glow / vignette / easing من makeWall) =====

    internal static float Clamp01(double x) => (float)Math.Min(1, Math.Max(0, x));
    internal static double Lerp(double a, double b, double k) => a + (b - a) * k;
    internal static double Ease(double k) => 1 - Math.Pow(1 - Clamp01(k), 3);
    internal static double EaseIO(double k) { k = Clamp01(k); return k < 0.5 ? 4 * k * k * k : 1 - Math.Pow(-2 * k + 2, 3) / 2; }
    internal static double Seg(double x, double a, double b) => Clamp01((x - a) / (b - a));
    internal static double Pulse(double dt, double len = 1.2) => dt < 0 || dt > len ? 0 : dt < 0.18 ? Ease(dt / 0.18) : 1 - EaseIO(Seg(dt, 0.18, len));

    internal static Func<double, double> Spring(double w0, double z) => t =>
    {
        if (t <= 0) return 0;
        var wd = w0 * Math.Sqrt(1 - z * z);
        return 1 - Math.Exp(-z * w0 * t) * (Math.Cos(wd * t) + z * w0 / wd * Math.Sin(wd * t));
    };

    internal static SKColor Rgba(string rgb, double a)
    {
        var p = rgb.Split(',');
        return new SKColor(byte.Parse(p[0]), byte.Parse(p[1]), byte.Parse(p[2]), (byte)Math.Round(Clamp01(a) * 255));
    }

    internal static SKColor Hsla(double h, float s, float l, double a = 1)
    {
        var c = SKColor.FromHsl((float)(((h % 360) + 360) % 360), s, l);
        return c.WithAlpha((byte)Math.Round(Clamp01(a) * 255));
    }

    internal SKPaint Fill(SKShader? shader = null, SKColor? color = null) => new()
    {
        IsAntialias = true, BlendMode = Blend, Shader = shader, Color = color ?? SKColors.White, IsDither = true,
    };

    internal void FillBg()
    {
        using var p = new SKPaint { Color = Bg, BlendMode = SKBlendMode.Src };
        Canvas.DrawRect(0, 0, W, H, p);
    }

    internal void Glow(double x, double y, double r, string rgb, double a)
    {
        if (a <= 0.002 || r <= 0) return;
        using var shader = SKShader.CreateRadialGradient(new SKPoint((float)x, (float)y), (float)r,
            new[] { Rgba(rgb, a), Rgba(rgb, a * 0.45), Rgba(rgb, 0) }, new[] { 0f, 0.4f, 1f }, SKShaderTileMode.Clamp);
        using var p = Fill(shader);
        Canvas.DrawRect((float)(x - r), (float)(y - r), (float)(r * 2), (float)(r * 2), p);
    }

    internal void Vignette(double a)
    {
        using var shader = SKShader.CreateRadialGradient(new SKPoint(CX, CY), 300,
            new[] { Bg.WithAlpha(0), Bg.WithAlpha((byte)Math.Round(Clamp01(a) * 255)) }, new[] { 80f / 300, 1f }, SKShaderTileMode.Clamp);
        using var p = new SKPaint { Shader = shader, BlendMode = SKBlendMode.SrcOver, IsDither = true };
        Canvas.DrawRect(0, 0, W, H, p);
    }

    internal void Dot(double x, double y, double r, SKColor c)
    {
        using var p = Fill(color: c);
        Canvas.DrawCircle((float)x, (float)y, (float)r, p);
    }

    internal float Rand() => (float)Rng.NextDouble();
}

internal interface IScene
{
    void Init();
    void OnTap((float X, float Y) p, double now);
    void Draw(double now, double t, double dt);
    /// تقدّم الحالة دون رسم (للمشاهد ذات الفيزياء)
    void Step(double now, double dt) { }
}
