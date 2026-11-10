using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class GameCard : UserControl
{
    private GameCardViewModel? _card;

    public GameCard()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_card != null) _card.PropertyChanged -= OnCardChanged;
            _card = DataContext as GameCardViewModel;
            if (_card != null) _card.PropertyChanged += OnCardChanged;
        };

        var item = this.FindControl<Button>("SettingsItem")!;
        var sliders = this.FindControl<SlidersIcon>("MenuSliders")!;
        item.PointerEntered += (_, _) => sliders.IsFlipped = true;
        item.PointerExited += (_, _) => sliders.IsFlipped = false;
    }

    private MainViewModel? MainVm => TopLevel.GetTopLevel(this)?.DataContext as MainViewModel;

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GameCardViewModel.IsMenuOpen) || _card == null) return;

        // البطاقة المفتوحة قائمتها فوق جاراتها
        if (this.GetVisualParent() is Control container)
            container.ZIndex = _card.IsMenuOpen ? 20 : 0;

        if (_card.IsMenuOpen)
        {
            var menu = this.FindControl<Border>("Menu")!;
            _ = MotionSettings.AnimateAsync(menu, TimeSpan.FromSeconds(0.24),
                new CssBezierEasing(0.34, 1.56, 0.64, 1), 0, 0.88, -4);
        }
    }

    private void Play_Click(object? sender, RoutedEventArgs e) =>
        MainVm?.LaunchGameCommand.Execute(_card);

    private void Menu_Click(object? sender, RoutedEventArgs e)
    {
        if (_card != null) MainVm?.ToggleCardMenu(_card);
    }

    private void Favorite_Click(object? sender, RoutedEventArgs e)
    {
        if (_card != null) MainVm?.ToggleFavoriteFromMenu(_card);
    }

    private void Settings_Click(object? sender, RoutedEventArgs e) =>
        MainVm?.OpenDetailsCommand.Execute(_card);

    private void Shortcut_Click(object? sender, RoutedEventArgs e)
    {
        if (_card != null) MainVm?.CreateDesktopShortcut(_card);
    }

    private async void CopyId_Click(object? sender, RoutedEventArgs e)
    {
        if (_card == null) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(_card.Game.Id);
        MainVm?.OnGameIdCopied(_card);
    }
}
