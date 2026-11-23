using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Library;
using HalawaHub.Core.Updates;
using HalawaHub.App.Services;

namespace HalawaHub.App;

public partial class App : Application
{
    internal HalawaHub.App.BigPicture.BigPictureController? BigPicture { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // الثيم ولون التمييز قبل إنشاء أي نافذة
        var config = ConfigService.Load();
        ThemeManager.Apply(this, config.Theme, config.Accent);
        MotionPreferences.Apply(config.ReduceMotion);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var libraryService = new LibraryService();
            var filterService = new FilterService();
            var updateService = new UpdateService(new UpdateChecker());
            var mainVm = new MainViewModel(libraryService, filterService, updateService);
            var window = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = window;

            // الشاشة الكبيرة: --big-picture يفتحها مباشرة بعد ظهور النافذة العادية
            BigPicture = new HalawaHub.App.BigPicture.BigPictureController(window, mainVm);
            var force = desktop.Args?.Contains("--big-picture") == true;
            // Opened يتكرر مع كل إظهار للنافذة: البدء مرة واحدة فقط
            var started = false;
            window.Opened += (_, _) =>
            {
                if (started) return;
                started = true;
                Dispatcher.UIThread.Post(() => BigPicture.Start(force), DispatcherPriority.Background);
            };
            desktop.Exit += (_, _) => BigPicture.Dispose();

            Program.InstanceGuard?.StartListening(() => Dispatcher.UIThread.Post(window.ShowAndFocus));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
