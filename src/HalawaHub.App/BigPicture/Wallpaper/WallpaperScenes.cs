using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using static HalawaHub.App.BigPicture.Wallpaper.WallpaperEngine;

namespace HalawaHub.App.BigPicture.Wallpaper;

// كل مشهد نقل حرفي لما في makeWall (BigPicture.dc.html)، بنفس الأرقام.

/// 1 فقاعات ناعمة: خمس فقاعات تنساب، والنقرة تجذب الأولى إليها وتبعثر البقية
internal sealed class Blobs : IScene
{
    private readonly WallpaperEngine e;
    private static readonly Func<double, double> Drift = Spring(4.2, 0.9);
    private sealed class B { public string K = ""; public float R, Ph; public (float X, float Y) From, To; public double X, Y; }
    private B[] _b = Array.Empty<B>();
    public Blobs(WallpaperEngine e) => this.e = e;

    public void Init() => _b = new (string, float, (float, float), float)[]
    {
        ("a", 175, (130, 90), 0), ("b", 135, (360, 80), 2.1f), ("c", 145, (280, 200), 4.2f), ("d", 95, (60, 200), 1.3f), ("e", 125, (430, 180), 3.4f),
    }.Select(x => new B { K = x.Item1, R = x.Item2, Ph = x.Item4, From = x.Item3, To = x.Item3, X = x.Item3.Item1, Y = x.Item3.Item2 }).ToArray();

    private (double X, double Y) Base(B b, double now)
    {
        var dt = e.Since(now);
        var k = Lerp(Drift(dt), 1, Seg(dt, 1.6, 2.2));
        return (Lerp(b.From.X, b.To.X, k), Lerp(b.From.Y, b.To.Y, k));
    }

    public void OnTap((float X, float Y) p, double now)
    {
        for (var i = 0; i < _b.Length; i++)
        {
            var (x, y) = Base(_b[i], now);
            _b[i].From = ((float)x, (float)y);
            _b[i].To = i == 0 ? p : (40 + e.Rand() * (W - 80), 30 + e.Rand() * (H - 60));
        }
    }

    private string Col(string k) => k switch { "a" => e.P.A, "b" => e.P.B, "c" => e.P.C, "d" => e.P.D, _ => e.P.E };

    public void Draw(double now, double T, double dt)
    {
        var sg = Pulse(e.Since(now), 1.4);
        e.FillBg();
        e.Blend = SKBlendMode.Screen;
        foreach (var b in _b)
        {
            var (bx, by) = Base(b, now);
            var c = Col(b.K);
            b.X = bx + 22 * Math.Sin(0.5 * T + b.Ph) + 8 * Math.Sin(1.1 * T + 2 * b.Ph);
            b.Y = by + 14 * Math.Cos(0.63 * T + b.Ph) + 6 * Math.Cos(1.3 * T + b.Ph);
            var r = b.R * (1 + 0.08 * Math.Sin(0.8 * T + b.Ph) + 0.1 * sg);
            var st = 0.18 * Math.Sin(0.4 * T + b.Ph);
            var a = (b.K == "d" ? 0.22 : 0.42) + 0.15 * sg;
            e.Canvas.Save();
            e.Canvas.Translate((float)b.X, (float)b.Y);
            e.Canvas.RotateRadians((float)(0.06 * T + b.Ph));
            e.Canvas.Scale((float)(1 + st), (float)(1 - 0.8 * st));
            using (var shader = SKShader.CreateRadialGradient(SKPoint.Empty, (float)r,
                       new[] { Rgba(c, a), Rgba(c, a * 0.6), Rgba(c, a * 0.16), Rgba(c, 0) }, new[] { 0f, 0.35f, 0.7f, 1f }, SKShaderTileMode.Clamp))
            using (var p = e.Fill(shader))
                e.Canvas.DrawRect((float)-r, (float)-r, (float)(2 * r), (float)(2 * r), p);
            e.Canvas.Restore();
        }
        e.Glow(_b[0].X, _b[0].Y, 60 * (1 + 0.2 * sg), e.P.D, 0.1 + 0.16 * sg);
        e.Vignette(0.55);
    }
}

/// 2 أشرطة الشفق: ثلاثة أشرطة متموجة فوق نجوم خافتة، والنقرة تشعلها
internal sealed class Aurora : IScene
{
    private readonly WallpaperEngine e;
    private (float X, float Y, float R, float Ph)[] _stars = Array.Empty<(float, float, float, float)>();
    public Aurora(WallpaperEngine e) => this.e = e;
    public void Init() => _stars = Enumerable.Range(0, 50).Select(_ => (e.Rand() * W, e.Rand() * H * 0.7f, 0.35f + e.Rand() * 0.75f, e.Rand() * MathF.PI * 2)).ToArray();
    public void OnTap((float X, float Y) p, double now) { }

    public void Draw(double now, double T, double dt)
    {
        var boost = Pulse(e.Since(now), 2.6);
        e.FillBg();
        foreach (var s in _stars) e.Dot(s.X, s.Y, s.R, new SKColor(255, 235, 230, (byte)(255 * Clamp01(0.15 + 0.15 * Math.Sin(1.5 * T + s.Ph)))));
        var k = 1 + 0.9 * boost;
        var edges = new List<(SKPath Path, string C)>();
        // نصف دقة ثم يُكبَّر بمزج الشاشة (كما في التصميم)
        using var off = SKSurface.Create(new SKImageInfo((int)(W / 2), (int)(H / 2), SKColorType.Rgba8888, SKAlphaType.Premul));
        var oc = off.Canvas;
        oc.Clear(SKColors.Transparent);
        oc.Scale(0.5f);
        foreach (var b in new (double y, double a, double f, double s, double ph, string c, double h)[] { (90.0, 18.0, 0.012, 0.5, 0.0, e.P.B, 70.0), (140.0, 24.0, 0.009, 0.37, 2.0, e.P.A, 85.0), (195.0, 16.0, 0.015, 0.62, 4.0, e.P.C, 60.0) })
        {
            var amp = b.a * (1 + 0.6 * boost);
            var yc = b.y + 8 * Math.Sin(0.3 * T + b.ph);
            var pts = new List<(double X, double Y, double Hh)>();
            for (var x = -16.0; x <= W + 16; x += 6)
                pts.Add((x, yc + amp * Math.Sin(x * b.f + T * b.s + b.ph) + 0.4 * amp * Math.Sin(x * b.f * 2.3 - T * b.s * 1.4 + b.ph),
                    b.h * (1 + 0.45 * boost) * (0.65 + 0.35 * Math.Sin(x * 0.045 + T * 0.7 + b.ph) * Math.Sin(x * 0.013 - T * 0.3))));
            foreach (var (x, y, hh) in pts)
            {
                var top = y - hh;
                using var rg = SKShader.CreateLinearGradient(new SKPoint(0, (float)top), new SKPoint(0, (float)y),
                    new[] { Rgba(b.c, 0), Rgba(b.c, Math.Min(0.25, 0.05 * k)), Rgba(b.c, Math.Min(0.5, 0.17 * k)) }, new[] { 0f, 0.6f, 1f }, SKShaderTileMode.Clamp);
                using var p = new SKPaint { Shader = rg, StrokeWidth = 6, Style = SKPaintStyle.Stroke, BlendMode = SKBlendMode.Plus, StrokeCap = SKStrokeCap.Butt };
                oc.DrawLine((float)x, (float)y, (float)x, (float)top, p);
            }
            var edge = new SKPath();
            edge.MoveTo((float)pts[0].X, (float)pts[0].Y);
            foreach (var pt in pts.Skip(1)) edge.LineTo((float)pt.X, (float)pt.Y);
            foreach (var (w, a) in new[] { (30.0, 0.08), (14.0, 0.14) })
            {
                using var p = new SKPaint { Color = Rgba(b.c, Math.Min(0.9, a * k)), StrokeWidth = (float)(w * (1 + 0.3 * boost)), Style = SKPaintStyle.Stroke,
                    StrokeCap = SKStrokeCap.Round, BlendMode = SKBlendMode.Plus, IsAntialias = true };
                oc.DrawPath(edge, p);
            }
            edges.Add((edge, b.c));
        }
        using (var img = off.Snapshot())
        using (var p = new SKPaint { BlendMode = SKBlendMode.Screen, FilterQuality = SKFilterQuality.Medium })
            e.Canvas.DrawImage(img, new SKRect(0, 0, W, H), p);
        e.Blend = SKBlendMode.Screen;
        foreach (var (edge, c) in edges)
        {
            foreach (var (w, a) in new[] { (5.0, 0.22), (1.8, 0.65) })
            {
                using var p = new SKPaint { Color = Rgba(c, Math.Min(0.95, a * k)), StrokeWidth = (float)(w * (1 + 0.3 * boost)), Style = SKPaintStyle.Stroke,
                    StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round, BlendMode = SKBlendMode.Screen, IsAntialias = true };
                e.Canvas.DrawPath(edge, p);
            }
            edge.Dispose();
        }
        e.Vignette(0.5);
    }
}

/// 3 حبيبات الفيلم: أضواء دافئة تنساب وحبيبات تتبدل 24 مرة في الثانية
internal sealed class Grain : IScene
{
    private readonly WallpaperEngine e;
    private SKBitmap[] _g = Array.Empty<SKBitmap>();
    private int _frame = -1, _idx;
    private (float X, float Y) _off;
    private double _flick = 1;
    public Grain(WallpaperEngine e) => this.e = e;

    public void Init()
    {
        _g = Enumerable.Range(0, 4).Select(_ =>
        {
            var bmp = new SKBitmap(256, 256, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            for (var y = 0; y < 256; y++)
                for (var x = 0; x < 256; x++)
                {
                    var v = e.Rand() - e.Rand();
                    var c = v > 0 ? (byte)255 : (byte)0;
                    bmp.SetPixel(x, y, new SKColor(c, c, c, (byte)Math.Min(255, Math.Abs(v) * 380)));
                }
            return bmp;
        }).ToArray();
    }

    public void OnTap((float X, float Y) p, double now) { }

    public void Draw(double now, double T, double dt)
    {
        var sg = Pulse(e.Since(now), 1.5);
        var f = (int)Math.Floor(T * 24);
        if (f != _frame) { _frame = f; _idx = ((f % 4) + 4) % 4; _off = (e.Rand() * 256, e.Rand() * 256); _flick = 0.95 + e.Rand() * 0.07; }
        e.FillBg();
        e.Blend = SKBlendMode.Screen;
        double x1 = 200 + 80 * Math.Sin(0.23 * T), y1 = 120 + 35 * Math.Sin(0.31 * T + 1), x2 = 310 + 70 * Math.Sin(0.19 * T + 2), y2 = 155 + 40 * Math.Cos(0.27 * T), fl = _flick;
        e.Glow(x1, y1, 220 * (1 + 0.1 * sg), e.P.A, (0.42 + 0.25 * sg) * fl);
        e.Glow(x2, y2, 160 * (1 + 0.1 * sg), e.P.C, (0.24 + 0.18 * sg) * fl);
        e.Glow((x1 + x2) / 2, (y1 + y2) / 2 - 10, 95 * (1 + 0.15 * sg), e.P.B, (0.18 + 0.2 * sg) * fl);
        e.Glow(-10 + 40 * Math.Sin(0.11 * T), CY + 100 * Math.Sin(0.07 * T), 190, "255,140,80", 0.12 * fl);
        e.Glow(e.Tap.X, e.Tap.Y, 110, e.P.D, 0.28 * sg);
        e.Vignette(0.6);
        // الحبيبات بدقة البكسل الحقيقي (السطح ضعف إحداثيات المشهد)
        e.Canvas.Save();
        e.Canvas.Scale(0.5f);
        using var shader = SKShader.CreateBitmap(_g[_idx], SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKMatrix.CreateTranslation(-_off.X, -_off.Y));
        using var p = new SKPaint { Shader = shader, Color = SKColors.White.WithAlpha((byte)(255 * Clamp01(0.06 + 0.12 * sg))), BlendMode = SKBlendMode.SrcOver };
        e.Canvas.DrawRect(0, 0, W * 2, H * 2, p);
        e.Canvas.Restore();
    }
}

/// 4 تموج النقاط: شبكة نقاط تلمع، والنقرة ترسل موجة دائرية
internal sealed class Dots : IScene
{
    private readonly WallpaperEngine e;
    private readonly List<float> _dx = new(), _dy = new();
    public Dots(WallpaperEngine e) => this.e = e;
    public void Init()
    {
        const float sp = 16;
        _dx.Clear(); _dy.Clear();
        for (var x = CX % sp; x <= W; x += sp) _dx.Add(x);
        for (var y = CY % sp; y <= H; y += sp) _dy.Add(y);
    }
    public void OnTap((float X, float Y) p, double now) { }

    public void Draw(double now, double T, double dt)
    {
        e.FillBg();
        e.Blend = SKBlendMode.Screen;
        e.Glow(CX, CY, 260, e.P.E, 0.3);
        var waves = new List<(double X, double Y, double R, double A)>();
        foreach (var tp in e.Taps)
        {
            var age = (now - tp.T) / 1000;
            if (age < 0 || age > 2) continue;
            var A = 1 - EaseIO(Seg(age, 0.1, 1.9));
            waves.Add((tp.X, tp.Y, 380 * Ease(age / 1.7), A));
            e.Glow(tp.X, tp.Y, 100, e.P.A, 0.35 * A * (1 - Seg(age, 0, 0.8)));
        }
        var lo = e.P.D.Split(',').Select(double.Parse).ToArray();
        foreach (var x in _dx)
            foreach (var y in _dy)
            {
                var fade = Math.Pow(Clamp01(1 - Math.Sqrt((x - CX) * (x - CX) + (y - CY) * (y - CY)) / 280), 1.2);
                double a = 0.03 + 0.24 * fade * (0.75 + 0.25 * Math.Sin(x * 0.035 + y * 0.02 - T * 1.1)), px = x, py = y, hot = 0;
                foreach (var w in waves)
                {
                    double dx = x - w.X, dy = y - w.Y, d = Math.Sqrt(dx * dx + dy * dy);
                    if (d == 0) d = 1;
                    double w1 = Math.Exp(-((d - w.R) * (d - w.R)) / 288), w2 = Math.Exp(-((d - w.R + 42) * (d - w.R + 42)) / 512);
                    hot += w.A * (w1 + 0.45 * w2);
                    px += dx / d * 5 * w1 * w.A;
                    py += dy / d * 5 * w1 * w.A;
                }
                a = Clamp01(a + 0.95 * hot);
                if (a < 0.02) continue;
                var k = Clamp01(hot);
                if (a > 0.3) e.Dot(px, py, 4.5 * a, Rgba(e.P.A, 0.18 * a));
                e.Dot(px, py, 0.8 + 1.5 * a, new SKColor((byte)Lerp(lo[0], 255, k), (byte)Lerp(lo[1], 255, k), (byte)Lerp(lo[2], 255, k), (byte)(255 * a)));
            }
        e.Vignette(0.35);
    }
}

/// 5 تدرج منساب: تدرج يدور ببطء وأضواء ناعمة، والنقرة تديره وتغير درجته قليلًا
internal sealed class Flow : IScene
{
    private readonly WallpaperEngine e;
    private double _angFrom, _angTo, _hueFrom, _hueTo;
    public Flow(WallpaperEngine e) => this.e = e;
    public void Init() { _hueFrom = _hueTo = e.P.H; _angFrom = _angTo = 0; }
    private double Turn(double now) => EaseIO(e.Since(now) / 1.6);

    public void OnTap((float X, float Y) p, double now)
    {
        var k = Turn(now);
        _angFrom = Lerp(_angFrom, _angTo, k);
        _hueFrom = Lerp(_hueFrom, _hueTo, k);
        _angTo = _angFrom + Math.PI / 3;
        _hueTo = e.P.H + (Math.Round((_hueTo - e.P.H) / 18 + 1) % 3) * 18;
    }

    public void Draw(double now, double T, double dt)
    {
        var sg = Pulse(e.Since(now), 1.4);
        var k = Turn(now);
        var ang = Lerp(_angFrom, _angTo, k) + 0.12 * Math.Sin(0.21 * T);
        var h = Lerp(_hueFrom, _hueTo, k) + 8 * Math.Sin(0.3 * T);
        double ca = Math.Cos(ang), sa = Math.Sin(ang);
        using (var g = SKShader.CreateLinearGradient(new SKPoint((float)(CX - ca * 290), (float)(CY - sa * 290)), new SKPoint((float)(CX + ca * 290), (float)(CY + sa * 290)),
                   new[] { Hsla(h, 60, 6), Hsla(h + 20, 65, 12), Hsla(h + 40, 60, 5) }, new[] { 0f, 0.5f, 1f }, SKShaderTileMode.Clamp))
        using (var p = new SKPaint { Shader = g, IsDither = true })
            e.Canvas.DrawRect(0, 0, W, H, p);
        e.Blend = SKBlendMode.Screen;
        foreach (var n in new (double ph, double fx, double fy, double r, double dh)[] { (0.0, 0.21, 0.17, 220.0, 0.0), (2.1, 0.16, 0.23, 190.0, 25.0), (4.2, 0.19, 0.13, 210.0, 45.0), (5.3, 0.25, 0.19, 150.0, -20.0) })
        {
            double ox = 170 * Math.Sin(n.fx * T + n.ph), oy = 75 * Math.Cos(n.fy * T + 1.3 * n.ph);
            double x = CX + ox * ca - 1.5 * oy * sa, y = CY + 0.5 * ox * sa + oy * ca, r = n.r * (1 + 0.08 * sg), a = 0.3 + 0.12 * sg;
            using var rg = SKShader.CreateRadialGradient(new SKPoint((float)x, (float)y), (float)r,
                new[] { Hsla(h + n.dh, 85, 52, a), Hsla(h + n.dh, 85, 48, a * 0.4), Hsla(h + n.dh, 85, 48, 0) }, new[] { 0f, 0.45f, 1f }, SKShaderTileMode.Clamp);
            using var p = e.Fill(rg);
            e.Canvas.DrawRect((float)(x - r), (float)(y - r), (float)(2 * r), (float)(2 * r), p);
        }
        e.Vignette(0.4);
    }
}

/// 6 خطوط الكنتور: خريطة ارتفاعات تتحرك، والنقرة ترفع تلة تضيء خطوطها
internal sealed class Contour : IScene
{
    private readonly WallpaperEngine e;
    private const float Cs = 6;
    private int _nx, _ny;
    private float[] _v = Array.Empty<float>();
    private readonly List<(float X, float Y, double T)> _hills = new();
    private static readonly int[][] SegT =
    {
        new int[0], new[] { 3, 2 }, new[] { 2, 1 }, new[] { 3, 1 }, new[] { 0, 1 }, new[] { 0, 1, 3, 2 }, new[] { 0, 2 }, new[] { 0, 3 },
        new[] { 0, 3 }, new[] { 0, 2 }, new[] { 0, 3, 2, 1 }, new[] { 0, 1 }, new[] { 3, 1 }, new[] { 2, 1 }, new[] { 3, 2 }, new int[0],
    };
    public Contour(WallpaperEngine e) => this.e = e;
    public void Init() { _nx = (int)Math.Ceiling(W / Cs) + 1; _ny = (int)Math.Ceiling(H / Cs) + 1; _v = new float[_nx * _ny]; _hills.Clear(); }
    private static double Amp((float X, float Y, double T) h, double now) { var a = (now - h.T) / 1000; return 1.5 * Ease(a / 1.1) * (1 - EaseIO(Seg(a, 3, 7))); }
    public void OnTap((float X, float Y) p, double now) { _hills.Add((p.X, p.Y, now)); if (_hills.Count > 4) _hills.RemoveAt(0); }

    private static (float, float) Ep(int edge, float x0, float y0, double a, double b, double c, double d, double lv) => edge switch
    {
        0 => ((float)(x0 + Cs * (lv - a) / (b - a)), y0),
        1 => (x0 + Cs, (float)(y0 + Cs * (lv - b) / (c - b))),
        2 => ((float)(x0 + Cs * (lv - d) / (c - d)), y0 + Cs),
        _ => (x0, (float)(y0 + Cs * (lv - a) / (d - a))),
    };

    public void Draw(double now, double T, double dt)
    {
        e.FillBg();
        var amps = _hills.Select(h => Amp(h, now)).ToArray();
        for (var j = 0; j < _ny; j++)
            for (var i = 0; i < _nx; i++)
            {
                double x = i * Cs, y = j * Cs;
                var v = 0.5 * Math.Sin(x * 0.018 + T * 0.12) + 0.5 * Math.Sin(y * 0.026 - T * 0.1 + x * 0.006) + 0.35 * Math.Sin((x + y) * 0.013 + T * 0.16);
                for (var k = 0; k < _hills.Count; k++)
                {
                    if (amps[k] < 0.001) continue;
                    double dx = x - _hills[k].X, dy = y - _hills[k].Y;
                    v += amps[k] * Math.Exp(-(dx * dx + dy * dy) / 3872);
                }
                _v[j * _nx + i] = (float)v;
            }
        var paths = Enumerable.Range(0, 3).Select(_ => new[] { new SKPath(), new SKPath() }).ToArray();
        var n = 0;
        for (var lv = -1.5; lv < 3.2; lv += 0.2, n++)
        {
            var pp = paths[lv < -0.2 ? 0 : lv < 1 ? 1 : 2][n % 4 == 0 ? 1 : 0];
            for (var j = 0; j < _ny - 1; j++)
                for (var i = 0; i < _nx - 1; i++)
                {
                    double a = _v[j * _nx + i], b = _v[j * _nx + i + 1], c = _v[(j + 1) * _nx + i + 1], d = _v[(j + 1) * _nx + i];
                    var idx = (a > lv ? 8 : 0) | (b > lv ? 4 : 0) | (c > lv ? 2 : 0) | (d > lv ? 1 : 0);
                    if (idx == 0 || idx == 15) continue;
                    var s = SegT[idx];
                    float x0 = i * Cs, y0 = j * Cs;
                    for (var m = 0; m < s.Length; m += 2)
                    {
                        var p1 = Ep(s[m], x0, y0, a, b, c, d, lv);
                        var p2 = Ep(s[m + 1], x0, y0, a, b, c, d, lv);
                        pp.MoveTo(p1.Item1, p1.Item2);
                        pp.LineTo(p2.Item1, p2.Item2);
                    }
                }
        }
        e.Blend = SKBlendMode.Screen;
        e.Glow(CX, CY, 280, e.P.E, 0.35);
        for (var i = 0; i < _hills.Count; i++) e.Glow(_hills[i].X, _hills[i].Y, 110, e.P.A, 0.3 * amps[i] / 1.5);
        var cols = new[] { e.P.E == "127,29,29" ? "190,60,60" : e.P.C, e.P.A, e.P.D };
        for (var b = 0; b < 3; b++)
        {
            using var thin = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f, Color = Rgba(cols[b], 0.17), IsAntialias = true, BlendMode = SKBlendMode.Screen, StrokeJoin = SKStrokeJoin.Round };
            using var thick = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1.1f, Color = Rgba(cols[b], 0.38), IsAntialias = true, BlendMode = SKBlendMode.Screen, StrokeJoin = SKStrokeJoin.Round };
            e.Canvas.DrawPath(paths[b][0], thin);
            e.Canvas.DrawPath(paths[b][1], thick);
        }
        var hk = _hills.Count - 1;
        if (hk >= 0 && amps[hk] > 0.01)
        {
            var h = _hills[hk];
            var a = amps[hk] / 1.5;
            using var g = SKShader.CreateRadialGradient(new SKPoint(h.X, h.Y), 120, new[] { Rgba(e.P.D, 0.7 * a), Rgba(e.P.D, 0) }, new[] { 0f, 1f }, SKShaderTileMode.Clamp);
            using var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1.1f, Shader = g, IsAntialias = true, BlendMode = SKBlendMode.Screen };
            foreach (var pp in paths) { e.Canvas.DrawPath(pp[0], p); e.Canvas.DrawPath(pp[1], p); }
        }
        foreach (var pp in paths) { pp[0].Dispose(); pp[1].Dispose(); }
        e.Vignette(0.4);
    }
}

/// 7 روابط النجوم: نجوم تتصل بخطوط، والنقرة تجذبها ثم تنثرها بحلقة
internal sealed class Stars : IScene
{
    private readonly WallpaperEngine e;
    private sealed class S { public double X, Y, Vx, Vy, Bx, By, R, Ph; }
    private sealed class F { public double X, Y, R, Z, Ph; }
    private S[] _st = Array.Empty<S>();
    private F[] _far = Array.Empty<F>();
    private bool _scattered = true;
    private double _scatterAt = -1e9;
    private const double Pull = 0.9;
    public Stars(WallpaperEngine e) => this.e = e;

    public void Init()
    {
        _st = Enumerable.Range(0, 80).Select(_ =>
        {
            var a = e.Rand() * Math.PI * 2;
            var sp = 4 + e.Rand() * 8;
            double vx = Math.Cos(a) * sp, vy = Math.Sin(a) * sp;
            return new S { X = e.Rand() * W, Y = e.Rand() * H, Vx = vx, Vy = vy, Bx = vx, By = vy, R = 0.6 + e.Rand() * 1.1, Ph = e.Rand() * Math.PI * 2 };
        }).ToArray();
        _far = Enumerable.Range(0, 90).Select(_ => new F { X = e.Rand() * W, Y = e.Rand() * H, R = 0.3 + e.Rand() * 0.5, Z = 0.3 + e.Rand() * 0.7, Ph = e.Rand() * Math.PI * 2 }).ToArray();
    }

    public void OnTap((float X, float Y) p, double now) => _scattered = false;

    public void Step(double now, double dt)
    {
        var age = e.Since(now);
        var (tx, ty) = e.Tap;
        var pulling = age >= 0 && age < Pull;
        if (!_scattered && age >= Pull)
        {
            _scattered = true;
            _scatterAt = now;
            foreach (var s in _st)
            {
                double dx = s.X - tx, dy = s.Y - ty, d = Math.Sqrt(dx * dx + dy * dy);
                if (d == 0) d = 1;
                if (d < 240) { var k = (1 - d / 240) * (160 + e.Rand() * 80); s.Vx += dx / d * k; s.Vy += dy / d * k; }
            }
        }
        foreach (var s in _st)
        {
            if (pulling)
            {
                double dx = tx - s.X, dy = ty - s.Y, d = Math.Sqrt(dx * dx + dy * dy);
                if (d == 0) d = 1;
                var f = 520 * Clamp01(1 - d / 260) * Clamp01(d / 20);
                s.Vx += dx / d * f * dt;
                s.Vy += dy / d * f * dt;
                var damp = Math.Exp(-2.5 * dt);
                s.Vx *= damp; s.Vy *= damp;
            }
            else
            {
                var k = 1 - Math.Exp(-1.4 * dt);
                s.Vx += (s.Bx - s.Vx) * k;
                s.Vy += (s.By - s.Vy) * k;
            }
            s.X += s.Vx * dt; s.Y += s.Vy * dt;
            if (s.X < 0) { s.X = 0; s.Vx = Math.Abs(s.Vx); s.Bx = Math.Abs(s.Bx); }
            if (s.X > W) { s.X = W; s.Vx = -Math.Abs(s.Vx); s.Bx = -Math.Abs(s.Bx); }
            if (s.Y < 0) { s.Y = 0; s.Vy = Math.Abs(s.Vy); s.By = Math.Abs(s.By); }
            if (s.Y > H) { s.Y = H; s.Vy = -Math.Abs(s.Vy); s.By = -Math.Abs(s.By); }
        }
        foreach (var s in _far) { s.X -= 4 * s.Z * dt; if (s.X < -2) s.X = W + 2; }
    }

    public void Draw(double now, double T, double dt)
    {
        Step(now, dt);
        var age = e.Since(now);
        var (tx, ty) = e.Tap;
        e.FillBg();
        foreach (var s in _far) e.Dot(s.X, s.Y, s.R, Rgba(e.P.D, (0.15 + 0.15 * Math.Sin(1.2 * T + s.Ph)) * s.Z));
        e.Blend = SKBlendMode.Screen;
        e.Glow(CX, CY, 260, e.P.E, 0.4);
        var tg = age >= 0 && age < 1.6 ? (age < Pull ? Seg(age, 0, 0.3) : 1 - EaseIO(Seg(age, Pull, 1.6))) : 0;
        e.Glow(tx, ty, 100, e.P.A, 0.32 * tg);
        var ra = (now - _scatterAt) / 1000;
        if (ra >= 0 && ra < 0.9)
        {
            using var p = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 1.2f, Color = Rgba(e.P.B, 0.35 * (1 - ra / 0.9)), IsAntialias = true, BlendMode = SKBlendMode.Screen };
            e.Canvas.DrawCircle(tx, ty, (float)(30 + 240 * Ease(ra / 0.9)), p);
        }
        using (var line = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f, IsAntialias = true, BlendMode = SKBlendMode.Screen })
        {
            for (var i = 0; i < _st.Length; i++)
                for (var j = i + 1; j < _st.Length; j++)
                {
                    S a = _st[i], b = _st[j];
                    var d = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                    if (d > 62) continue;
                    var k = 1 - d / 62;
                    var hot = tg * Clamp01(1 - Math.Sqrt(Math.Pow((a.X + b.X) / 2 - tx, 2) + Math.Pow((a.Y + b.Y) / 2 - ty, 2)) / 130);
                    line.Color = Rgba(hot > 0.3 ? e.P.A : e.P.D, (0.22 + 0.4 * hot) * k * k);
                    e.Canvas.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, line);
                }
        }
        foreach (var s in _st)
        {
            var a = 0.55 + 0.45 * Math.Sin(2 * T + s.Ph);
            if (s.R > 1.2) e.Dot(s.X, s.Y, s.R * 3.5, Rgba(e.P.A, 0.14 * a));
            e.Dot(s.X, s.Y, s.R, new SKColor(255, 245, 240, (byte)(255 * Clamp01(a))));
        }
        e.Vignette(0.4);
    }
}

/// 8 الأضواء المسرحية: ثلاثة أضواء تتأرجح، والنقرة توجهها إليها مع بقعة ضوء
internal sealed class Spot : IScene
{
    private readonly WallpaperEngine e;
    private static readonly Func<double, double> Glide = Spring(8, 0.8);
    private sealed class Beam { public (float X, float Y) O; public double Base, From, Target, Ph; public string K = ""; }
    private sealed class D { public double X, Y, Ph, Sp, R; }
    private Beam[] _be = Array.Empty<Beam>();
    private D[] _dust = Array.Empty<D>();
    private static readonly (double Spread, double A)[] Layers = Enumerable.Range(0, 10).Select(i => (0.26 * Math.Pow(1 - i / 10.0, 1.4) + 0.012, 0.03 + 0.004 * i)).ToArray();
    public Spot(WallpaperEngine e) => this.e = e;

    public void Init()
    {
        _be = new[]
        {
            new Beam { O = (80, -40), Base = Math.PI / 2 - 0.32, K = "a", Ph = 0 },
            new Beam { O = (CX, -50), Base = Math.PI / 2, K = "b", Ph = 2 },
            new Beam { O = (400, -40), Base = Math.PI / 2 + 0.32, K = "c", Ph = 4 },
        };
        foreach (var b in _be) { b.From = b.Base; b.Target = b.Base; }
        _dust = Enumerable.Range(0, 110).Select(_ => new D { X = e.Rand() * W, Y = e.Rand() * H, Ph = e.Rand() * Math.PI * 2, Sp = 0.2 + e.Rand() * 0.5, R = 0.4 + e.Rand() * 0.7 }).ToArray();
    }

    private static double Idle(Beam b, double now) => b.Base + 0.35 * Math.Sin(0.45 * now / 1000 + b.Ph);
    private (double K, double Back) Aim(double now) { var dt = e.Since(now); return (Lerp(Glide(dt), 1, Seg(dt, 0.8, 1.2)), EaseIO(Seg(dt, 2.6, 4.8))); }
    private double Ang(Beam b, double now) { var (k, back) = Aim(now); return Lerp(Lerp(b.From, b.Target, k), Idle(b, now), back); }
    private string Col(string k) => k switch { "a" => e.P.A, "b" => e.P.B, _ => e.P.C };

    public void OnTap((float X, float Y) p, double now)
    {
        foreach (var b in _be) { b.From = Ang(b, now); b.Target = Math.Atan2(p.Y - b.O.Y, p.X - b.O.X); }
    }

    public void Step(double now, double dt)
    {
        var T = now / 1000;
        foreach (var d in _dust)
        {
            d.X += Math.Sin(T * d.Sp + d.Ph) * 4 * dt;
            d.Y -= (2 + 3 * d.Sp) * dt;
            if (d.Y < -2) { d.Y = H + 2; d.X = e.Rand() * W; }
        }
    }

    private void Cone(Beam b, double ang, double spread, double len, double a)
    {
        var (ox, oy) = b.O;
        var c = Col(b.K);
        using var g = SKShader.CreateLinearGradient(new SKPoint(ox, oy), new SKPoint((float)(ox + Math.Cos(ang) * len), (float)(oy + Math.Sin(ang) * len)),
            new[] { Rgba(c, a), Rgba(c, a * 0.45), Rgba(c, 0) }, new[] { 0f, 0.55f, 1f }, SKShaderTileMode.Clamp);
        using var path = new SKPath();
        path.MoveTo(ox, oy);
        path.LineTo((float)(ox + Math.Cos(ang - spread) * len), (float)(oy + Math.Sin(ang - spread) * len));
        path.LineTo((float)(ox + Math.Cos(ang + spread) * len), (float)(oy + Math.Sin(ang + spread) * len));
        path.Close();
        using var p = e.Fill(g);
        e.Canvas.DrawPath(path, p);
    }

    public void Draw(double now, double T, double dt)
    {
        var sg = Pulse(e.Since(now));
        var boost = 1 + 0.6 * sg;
        e.FillBg();
        e.Blend = SKBlendMode.Screen;
        var angs = _be.Select(b => Ang(b, now)).ToArray();
        for (var i = 0; i < _be.Length; i++)
        {
            foreach (var (sp, a) in Layers) Cone(_be[i], angs[i], sp, 400, a * boost);
            e.Glow(_be[i].O.X, 0, 70, Col(_be[i].K), 0.22 * boost);
        }
        Step(now, dt);
        foreach (var d in _dust)
        {
            double lit = 0;
            for (var i = 0; i < _be.Length; i++)
            {
                double dx = d.X - _be[i].O.X, dy = d.Y - _be[i].O.Y, da = Math.Atan2(dy, dx) - angs[i];
                lit += Math.Exp(-(da * da) / 0.012) * Clamp01(1 - Math.Sqrt(dx * dx + dy * dy) / 400);
            }
            e.Dot(d.X, d.Y, d.R, new SKColor(255, 240, 235, (byte)(255 * Clamp01(0.05 + 0.7 * Clamp01(lit) * boost))));
        }
        var (k, back) = Aim(now);
        var pool = Clamp01(k) * (1 - back);
        if (pool > 0.01)
        {
            e.Canvas.Save();
            e.Canvas.Translate(e.Tap.X, e.Tap.Y);
            e.Canvas.Scale(1, 0.5f);
            e.Glow(0, 0, 90, e.P.D, 0.3 * pool * boost);
            e.Glow(0, 0, 40, "255,255,255", 0.16 * pool * boost);
            e.Canvas.Restore();
        }
        e.Vignette(0.45);
    }
}
