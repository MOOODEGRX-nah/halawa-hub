using System.Diagnostics;
using Avalonia;
using HalawaHub.App.Services;
using HalawaHub.Core;
using HalawaHub.Core.Updates;

namespace HalawaHub.App;

class Program
{
    /// تبدأ App الاستماع عليه بعد إنشاء النافذة
    internal static SingleInstanceGuard? InstanceGuard { get; private set; }

    [STAThread]
        public static void Main(string[] args)
    {
        // قبل كل شيء: CI والتحديث الذاتي يقرآن الإصدار بدون شاشة وبدون لمس النسخة العاملة
        if (args.Contains("--version"))
        {
            Console.WriteLine(AppInfo.Version);
            return;
        }

        InstallInfo.ScrubAppImageEnvironment();

        // بعد تحديث لينكس: النسخة القديمة تشغّلنا ثم تخرج، فننتظر تحرير قفل النسخة الواحدة
        var afterUpdate = TryReadWaitForPid(args, out var previousPid);
        if (afterUpdate) WaitForExit(previousPid);

        // نسخة تعمل أصلًا (ربما مخفية بالصينية): نطلب منها إظهار نافذتها ونخرج
        using var guard = SingleInstanceGuard.Acquire();
        if (!guard.IsPrimary)
        {
            guard.SignalPrimary();
            return;
        }
        InstanceGuard = guard;

        if (afterUpdate) Task.Run(SelfUpdater.CleanLinuxLeftovers);

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


    internal static bool TryReadWaitForPid(string[] args, out int pid)
    {
        pid = 0;
        var index = Array.IndexOf(args, SelfUpdater.WaitForPidArgument);
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out pid);
    }

    private static void WaitForExit(int pid)
    {
        try
        {
            using var previous = Process.GetProcessById(pid);
            previous.WaitForExit(30_000);
        }
        catch (ArgumentException)
        {
            // خرجت قبل أن نبدأ
        }
        catch (Exception ex)
        {
            Log.Error("could not wait for the previous instance", ex);
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
