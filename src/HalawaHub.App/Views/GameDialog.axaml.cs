using System;
using System.ComponentModel;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class GameDialog : UserControl
{
    private MainViewModel? _vm;
    private readonly Border _box;
    private readonly Border _scrim;
    private readonly Control _title;
    private readonly Control _body;
    private int _lastSection;

    public GameDialog()
    {
        InitializeComponent();

        _box = this.FindControl<Border>("DialogBox")!;
        _scrim = this.FindControl<Border>("Scrim")!;
        _title = this.FindControl<Control>("Title")!;
        _body = this.FindControl<Control>("Body")!;

        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnViewModelChanged;
            _vm = DataContext as MainViewModel;
            if (_vm != null) _vm.PropertyChanged += OnViewModelChanged;
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDetailsOpen))
        {
            if (_vm!.IsDetailsOpen)
            {
                _lastSection = 0;
                _ = MotionSettings.AnimateAsync(_scrim, TimeSpan.FromSeconds(0.25), new CubicEaseOut(), 0, 1, 0);
                _ = MotionSettings.AnimateAsync(_box, TimeSpan.FromSeconds(0.4), MotionSettings.DialogEasing, 0, 0.94, 16);
            }
            else
            {
                _ = MotionSettings.AnimateAsync(_scrim, TimeSpan.FromSeconds(0.23), new CubicEaseIn(), 1, 1, 0, toOpacity: 0);
                _ = MotionSettings.AnimateAsync(_box, TimeSpan.FromSeconds(0.22), new CubicEaseIn(), 1, 1, 0,
                    toOpacity: 0, toScale: 0.96, toY: 10);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.GameDialogSection) && _vm!.GameDialogSection >= 0)
        {
            // المحتوى يدخل من الأسفل أو الأعلى حسب اتجاه التنقل
            var section = _vm.GameDialogSection;
            var offset = section >= _lastSection ? 14 : -14;
            _lastSection = section;
            var duration = TimeSpan.FromSeconds(0.38);
            _ = MotionSettings.AnimateAsync(_title, duration, MotionSettings.EnterEasing, 0, 1, offset);
            _ = MotionSettings.AnimateAsync(_body, duration, MotionSettings.EnterEasing, 0, 1, offset);
        }
    }

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        _vm?.CloseDetailsCommand.Execute(null);

    private async void CopyPath_Click(object? sender, RoutedEventArgs e)
    {
        var path = _vm?.DetailsGame?.InstallPath;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (!string.IsNullOrEmpty(path) && clipboard != null)
            await clipboard.SetTextAsync(path);
    }

    // منتقي الملفات تفاعل مع النظام، فمكانه طبقة الواجهة لا الـ ViewModel
    private async void ChangeCover_Click(object? sender, RoutedEventArgs e)
    {
        var card = _vm?.DetailsGame;
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (card == null || storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.Instance["chooseCover"],
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
            card.SetCustomCover(files[0].Path.LocalPath);
    }
}
