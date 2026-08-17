using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

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

    // يفتح منتقي ملفات عشان يختار المستخدم صورة غلاف مخصصة — تفاعل مع نظام
    // الملفات هذا أنسب مكان له بطبقة الواجهة نفسها بدل الـ ViewModel
    private async void ChangeCoverButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.DetailsGame == null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "اختر صورة الغلاف",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
        {
            vm.DetailsGame.SetCustomCover(files[0].Path.LocalPath);
        }
    }
}
