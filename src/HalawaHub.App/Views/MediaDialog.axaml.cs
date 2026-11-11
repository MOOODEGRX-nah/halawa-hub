using Avalonia.Controls;
using Avalonia.Input;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class MediaDialog : UserControl
{
    public MediaDialog() => InitializeComponent();

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as MainViewModel)?.CloseMediaCommand.Execute(null);
}
