using Avalonia;
using HalawaHub.Core;

namespace HalawaHub.App;

class Program
{
    [STAThread]
        public static void Main(string[] args)
    {
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
