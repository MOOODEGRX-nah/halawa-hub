namespace HalawaHub.App.Services;

/// حجم/حالة نافذة البرنامج المحفوظة بين مرات التشغيل
public class WindowSettings
{
    public double Width { get; set; } = 980;
    public double Height { get; set; } = 660;
    public bool IsMaximized { get; set; }
}
