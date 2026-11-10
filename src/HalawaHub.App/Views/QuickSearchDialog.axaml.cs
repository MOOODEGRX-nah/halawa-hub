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
            // عند الفتح: التركيز في حقل البحث فورًا (حتى لا تضيع الأحرف المكتوبة مباشرة بعد Ctrl+K)،
            // ومرة ثانية بعد التخطيط، مع تظليل أول نتيجة
            if (e.Property != IsVisibleProperty || !IsVisible) return;
            QuickInput.Focus();
            Dispatcher.UIThread.Post(() =>
            {
                QuickInput.Focus();
                if (DataContext is MainViewModel vm) QuickResults.SelectedIndex = vm.QuickSearchIndex;
            }, DispatcherPriority.Loaded);
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
