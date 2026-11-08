using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HalawaHub.App.Controls;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class TopBar : UserControl
{
    private MainViewModel? _vm;

    public TopBar()
    {
        InitializeComponent();

        var button = this.FindControl<Button>("SettingsButton")!;
        button.PointerEntered += (_, _) => UpdateIcon(true);
        button.PointerExited += (_, _) => UpdateIcon(false);

        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnViewModelChanged;
            _vm = DataContext as MainViewModel;
            if (_vm != null) _vm.PropertyChanged += OnViewModelChanged;
        };
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSettingsVisible))
            UpdateIcon(this.FindControl<Button>("SettingsButton")!.IsPointerOver);
    }

    private void UpdateIcon(bool hovering)
    {
        this.FindControl<SlidersIcon>("Sliders")!.IsFlipped = hovering || _vm?.IsSettingsVisible == true;
    }
}
