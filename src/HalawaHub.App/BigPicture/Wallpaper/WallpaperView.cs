using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace HalawaHub.App.BigPicture.Wallpaper;

/// <summary>
/// سطح واحد خلف صفحات الشاشة الكبيرة يرسم الخلفية المتحركة. يُرسم بنصف الدقة (960×540) ثم يُكبَّر.
/// الحركة ثلاثة أوضاع (7.4): move يعمل نحو 2.5 ثانية بعد كل تنقل أو A ثم يتوقف تمامًا، always دائمًا،
/// still إطار واحد. بلا إطارات مطلوبة لا يستهلك شيئًا (قاعدة الخمول من v0.0.10.66).
/// </summary>
public sealed class WallpaperView : Control
{
    public const int PixelW = 960, PixelH = 540;
    /// مدة الحركة بعد تنقل أو A في وضع «عندما تتحرك»
    public static readonly TimeSpan WakeAfterMove = TimeSpan.FromSeconds(2.5);

    private readonly WallpaperEngine _engine = new();
    private WriteableBitmap? _bitmap;
    private string _mode = "move";
    private DateTime _runUntil = DateTime.MinValue;
    private bool _frameRequested;
    private DateTime _last = DateTime.MinValue;

    /// عدد الإطارات المرسومة (للاختبارات وقياس الخمول)
    public int FramesDrawn { get; private set; }
    public bool IsRunning => _frameRequested;
    public string SceneId => _engine.SceneId;

    /// يضبط المشهد والألوان والوضع والتفاعل، ويرسم إطارًا جديدًا
    public void Configure(string id, string accent, bool oled, string mode, bool react)
    {
        var sceneChanged = id != _engine.SceneId;
        _engine.SetScene(id);
        _engine.SetColors(accent, oled);
        _engine.React = react;
        _mode = mode;
        if (sceneChanged) _engine.Advance(2);   // كما يبدأ المشهد في التصميم بعد ثانيتين من الحركة
        Wake(sceneChanged ? 2.5 : 0.6);
    }

    /// نقرة عند مركز العنصر المركَّز، بإحداثيات هذا العنصر
    public void TapAt(Point p)
    {
        var b = Bounds;
        if (b.Width <= 0 || b.Height <= 0) return;
        if (_mode != "still") _engine.OnTap((float)(p.X / b.Width * WallpaperEngine.W), (float)(p.Y / b.Height * WallpaperEngine.H));
        Wake(2.6);
    }

    /// تنقل بلا نقرة: يتحرك قليلًا ثم يتوقف
    public void Nudge() => Wake(0.9);

    private void Wake(double seconds)
    {
        if (_mode == "still")
        {
            RequestFrame();
            return;
        }
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
        if (until > _runUntil) _runUntil = until;
        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_frameRequested) return;
        if (TopLevel.GetTopLevel(this) is not { } top) { DrawFrame(0); InvalidateVisual(); return; }
        _frameRequested = true;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan _)
    {
        _frameRequested = false;
        var now = DateTime.UtcNow;
        var dt = _last == DateTime.MinValue ? 0 : Math.Min(0.05, (now - _last).TotalSeconds);
        _last = now;
        DrawFrame(dt);
        InvalidateVisual();
        if (_mode == "always" || (_mode == "move" && now < _runUntil)) RequestFrame();
        else _last = DateTime.MinValue;   // توقف تمامًا حتى الطلب التالي
    }

    /// يرسم إطارًا في الصورة (نصف الدقة)
    internal void DrawFrame(double dt)
    {
        _bitmap ??= new WriteableBitmap(new PixelSize(PixelW, PixelH), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = _bitmap.Lock())
        {
            var info = new SKImageInfo(PixelW, PixelH, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, fb.Address, fb.RowBytes);
            _engine.Frame(surface.Canvas, PixelW, PixelH, dt);
            surface.Canvas.Flush();
        }
        FramesDrawn++;
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap == null) return;
        context.DrawImage(_bitmap, new Rect(0, 0, PixelW, PixelH), new Rect(Bounds.Size));
    }

    /// معاينة صغيرة للمشهد (لبطاقات الاختيار في الإعدادات)
    public static Bitmap Preview(string id, string accent, bool oled, int width = 192, int height = 108)
    {
        var engine = new WallpaperEngine(seed: 11);
        engine.SetScene(id);
        engine.SetColors(accent, oled);
        engine.Advance(3);
        var bmp = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var fb = bmp.Lock();
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), fb.Address, fb.RowBytes);
        engine.Frame(surface.Canvas, width, height, 0);
        surface.Canvas.Flush();
        return bmp;
    }
}
