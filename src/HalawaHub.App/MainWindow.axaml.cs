using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetupTrayAndCloseBehavior();

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

        // Tunnel: يصل قبل أن يستهلك أي زر الضغطة
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel);

        Loc.Instance.LanguageChanged += (_, _) => _ = FadeContentAsync();
    }

    private async System.Threading.Tasks.Task FadeContentAsync()
    {
        var page = this.FindControl<Grid>("PageContent");
        if (page != null)
            await MotionSettings.AnimateAsync(page, System.TimeSpan.FromSeconds(0.35),
                new Avalonia.Animation.Easings.CubicEaseOut(), 0.35, 1, 0);
    }

    /// قائمة البطاقة تُغلق عند الضغط في أي مكان خارجها
    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var insideMenu = (e.Source as Control)?.GetSelfAndVisualAncestors()
            .OfType<Control>()
            .Any(c => c.Classes.Contains("card-menu") || c.Classes.Contains("eb")) == true;

        if (!insideMenu)
            vm.CloseAllCardMenus();
    }

    /// Esc يغلق الأقرب: القائمة، ثم البحث، ثم النافذة المفتوحة
    private void OnAnyKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        // Ctrl+K: بحث سريع من أي صفحة
        if (e.Key == Key.K && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            if (vm.IsQuickSearchOpen) vm.CloseQuickSearch();
            else if (!vm.IsConfirmOpen) vm.OpenQuickSearch();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape) return;

        if (vm.IsQuickSearchOpen)
        {
            vm.CloseQuickSearch();
            e.Handled = true;
            return;
        }

        if (vm.IsConfirmOpen)
        {
            vm.CancelConfirmCommand.Execute(null);
            e.Handled = true;
        }
        else if (vm.CloseAllCardMenus())
        {
            e.Handled = true;
        }
        else if (vm.IsChangelogOpen)
        {
            vm.CloseChangelogCommand.Execute(null);
            e.Handled = true;
        }
        else if (vm.IsDetailsOpen)
        {
            vm.CloseDetailsCommand.Execute(null);
            e.Handled = true;
        }
        else if (vm.IsSettingsOpen)
        {
            vm.CloseSettingsCommand.Execute(null);
            e.Handled = true;
        }
        else if (vm.IsSearchOpen)
        {
            vm.CloseSearch();
            e.Handled = true;
        }
    }
}
