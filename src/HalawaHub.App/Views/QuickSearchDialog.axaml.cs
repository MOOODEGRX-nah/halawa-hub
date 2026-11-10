using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class QuickSearchDialog : UserControl
{
    public QuickSearchDialog()
    {
        InitializeComponent();
        QuickInput.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        QuickResults.DoubleTapped += (_, _) => (DataContext as MainViewModel)?.ConfirmQuickSearch();
        PropertyChanged += (_, e) =>
        {
            // عند الفتح: التركيز في حقل البحث مباشرة
            if (e.Property == IsVisibleProperty && IsVisible)
                Dispatcher.UIThread.Post(() => QuickInput.Focus(), DispatcherPriority.Input);
        };
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        switch (e.Key)
        {
            case Key.Down:
                vm.MoveQuickSearch(1);
                e.Handled = true;
                break;
            case Key.Up:
                vm.MoveQuickSearch(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                vm.ConfirmQuickSearch();
                e.Handled = true;
                break;
        }
    }

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as MainViewModel)?.CloseQuickSearch();
}
