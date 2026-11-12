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
            var window = new MainWindow
            {
                DataContext = new MainViewModel(libraryService, filterService, updateService)
            };
            desktop.MainWindow = window;

            Program.InstanceGuard?.StartListening(() => Dispatcher.UIThread.Post(window.ShowAndFocus));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
