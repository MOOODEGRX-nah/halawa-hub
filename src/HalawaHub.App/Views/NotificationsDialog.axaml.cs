using Avalonia.Controls;
using Avalonia.Input;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class NotificationsDialog : UserControl
{
    public NotificationsDialog() => InitializeComponent();

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as MainViewModel)?.CloseNotifications();
}
