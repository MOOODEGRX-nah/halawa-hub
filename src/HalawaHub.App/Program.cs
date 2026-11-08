using Avalonia;
using HalawaHub.App.Services;
using HalawaHub.Core;

namespace HalawaHub.App;

class Program
{
    /// تبدأ App الاستماع عليه بعد إنشاء النافذة
    internal static SingleInstanceGuard? InstanceGuard { get; private set; }

    [STAThread]
        public static void Main(string[] args)
    {
        // نسخة تعمل أصلًا (ربما مخفية بالصينية): نطلب منها إظهار نافذتها ونخرج
        using var guard = SingleInstanceGuard.Acquire();
        if (!guard.IsPrimary)
        {
            guard.SignalPrimary();
            return;
        }
        InstanceGuard = guard;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error("استثناء غير معالج أدى لخروج البرنامج", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("استثناء بمهمة خلفية ما تمت ملاحظته", e.Exception);
            e.SetObserved();
        };

        Log.Info($"=== بدء تشغيل Halawa-Hub v{AppInfo.Version} ===");

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Error("خطأ قاتل أثناء تشغيل البرنامج", ex);
            throw;
        }
        finally
        {
            Log.Info("=== خروج البرنامج ===");
        }
    }


    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
