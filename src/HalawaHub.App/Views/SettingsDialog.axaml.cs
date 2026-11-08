using System;
using System.ComponentModel;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class SettingsDialog : UserControl
{
    private MainViewModel? _vm;
    private readonly Border _box;
    private readonly Border _scrim;
    private readonly Control _title;
    private readonly Control _body;
    private int _lastSection;

    public SettingsDialog()
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

    private System.Threading.CancellationTokenSource? _spinner;

    /// دائرة الانتظار داخل زر "تحقق من التحديثات" (.chk.busy .spin)
    private void StartSpinner()
    {
        _spinner?.Cancel();
        _spinner = null;
        if (!MotionSettings.Enabled) return;

        _spinner = new System.Threading.CancellationTokenSource();
        _ = MotionSettings.RunLoopAsync(new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(0.8),
            Children =
            {
                new Avalonia.Animation.KeyFrame { Cue = new Avalonia.Animation.Cue(0), Setters = { new Avalonia.Styling.Setter(Avalonia.Media.RotateTransform.AngleProperty, 0d) } },
                new Avalonia.Animation.KeyFrame { Cue = new Avalonia.Animation.Cue(1), Setters = { new Avalonia.Styling.Setter(Avalonia.Media.RotateTransform.AngleProperty, 360d) } },
            }
        }, this.FindControl<Panel>("SpinRing")!, _spinner.Token);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSettingsOpen))
        {
            if (_vm!.IsSettingsOpen)
            {
                _ = MotionSettings.AnimateAfterLayoutAsync(_scrim, TimeSpan.FromSeconds(0.25), new CubicEaseOut(), 0, 1, 0);
                _ = MotionSettings.AnimateAfterLayoutAsync(_box, TimeSpan.FromSeconds(0.42), MotionSettings.DialogEasing, 0, 0.94, 18);
            }
            else
            {
                _ = MotionSettings.AnimateAsync(_scrim, TimeSpan.FromSeconds(0.23), new CubicEaseIn(), 1, 1, 0, toOpacity: 0);
                _ = MotionSettings.AnimateAsync(_box, TimeSpan.FromSeconds(0.22), new CubicEaseIn(), 1, 1, 0,
                    toOpacity: 0, toScale: 0.96, toY: 10);
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.UpdateKind) && _vm!.IsCheckingForUpdates)
        {
            StartSpinner();
        }
        else if (e.PropertyName == nameof(MainViewModel.UpdateKind))
        {
            _spinner?.Cancel();
            _spinner = null;

            var label = this.FindControl<Control>("UpdateStatusLabel")!;
            _ = MotionSettings.AnimateAsync(label, TimeSpan.FromSeconds(0.45),
                new CssBezierEasing(0.34, 1.56, 0.64, 1), 0, 0.85, 0);
        }
        else if (e.PropertyName == nameof(MainViewModel.SettingsSection))
        {
            var section = _vm!.SettingsSection;
            var offset = section >= _lastSection ? 16 : -16;
            _lastSection = section;
            var duration = TimeSpan.FromSeconds(0.4);
            _ = MotionSettings.AnimateAsync(_title, duration, MotionSettings.EnterEasing, 0, 1, offset);
            _ = MotionSettings.AnimateAsync(_body, duration, MotionSettings.EnterEasing, 0, 1, offset);
        }
    }

    private void Scrim_PointerPressed(object? sender, PointerPressedEventArgs e) =>
        _vm?.CloseSettingsCommand.Execute(null);
}
