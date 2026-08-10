using Avalonia.Controls;
using HalawaHub.App.Services;

namespace HalawaHub.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // نرجّع آخر حجم/حالة محفوظة للنافذة بدل ما تفتح دايمًا بنفس الحجم الافتراضي
        var settings = SettingsService.Load();

        if (settings.Width > 100 && settings.Height > 100)
        {
            Width = settings.Width;
            Height = settings.Height;
        }

        if (settings.IsMaximized)
            WindowState = WindowState.Maximized;

        Closing += (_, _) =>
        {
            SettingsService.Save(new WindowSettings
            {
                Width = WindowState == WindowState.Normal ? Width : settings.Width,
                Height = WindowState == WindowState.Normal ? Height : settings.Height,
                IsMaximized = WindowState == WindowState.Maximized
            });
        };
    }
}
