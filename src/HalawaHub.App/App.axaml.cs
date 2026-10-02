using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Library;
using HalawaHub.App.Services;

namespace HalawaHub.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var libraryService = new LibraryService();
            var filterService = new FilterService();
            var updateService = new UpdateService(new UpdateChecker());
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(libraryService, filterService, updateService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
