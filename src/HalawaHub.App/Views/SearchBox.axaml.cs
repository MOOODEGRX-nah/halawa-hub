using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class SearchBox : UserControl
{
    private const double ClosedWidth = 44;
    private const double OpenWidth = 340;

    private MainViewModel? _vm;
    private readonly Panel _box;
    private readonly Border _fill;
    private readonly Border _shell;
    private readonly TextBox _input;
    private readonly VectorIcon _icon;
    private int _blurToken;

    public SearchBox()
    {
        InitializeComponent();

        _box = this.FindControl<Panel>("Box")!;
        _fill = this.FindControl<Border>("Fill")!;
        _shell = this.FindControl<Border>("Shell")!;
        _input = this.FindControl<TextBox>("Input")!;
        _icon = this.FindControl<VectorIcon>("SearchIcon")!;

        _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        _input.LostFocus += OnInputLostFocus;
        _input.GotFocus += (_, _) => _blurToken++;

        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnViewModelChanged;
            _vm = DataContext as MainViewModel;
            if (_vm != null) _vm.PropertyChanged += OnViewModelChanged;
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSearchOpen))
            ApplyOpenState(_vm!.IsSearchOpen);
        else if (e.PropertyName == nameof(MainViewModel.SearchNoMatchText) && _vm!.ShowSearchNoMatch)
            _ = ShakeIconAsync();
    }

    private void ApplyOpenState(bool open)
    {
        if (MotionSettings.Enabled)
        {
            // فتح بنابض، وإغلاق أهدأ
            _box.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = WidthProperty,
                    Duration = TimeSpan.FromSeconds(open ? 0.42 : 0.35),
                    Easing = open ? new SplineEasing(0.34, 1.56, 0.64, 1) : new SplineEasing(0.65, 0, 0.35, 1)
                }
            };
        }

        _box.Width = open ? OpenWidth : ClosedWidth;
        _fill.Opacity = open ? 0 : 1;
        _shell.Opacity = open ? 1 : 0;
        _icon.Foreground = open ? this.FindResource("MutedBrush") as IBrush : Brushes.White;

        if (open)
            Dispatcher.UIThread.Post(() => _input.Focus(), DispatcherPriority.Background);
    }

    private void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm != null) _vm.IsSearchOpen = true;
    }

    private void Clear_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.SearchQuery = "";
        _input.Focus();
    }

    private void Recent_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is string text) Commit(text);
    }

    private void Suggestion_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is SearchSuggestion suggestion) Commit(suggestion.Text);
    }

    private void Commit(string text)
    {
        _vm?.CommitSearch(text);
        _input.Focus();
        _input.CaretIndex = text.Length;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm?.CommitSearch();
            e.Handled = true;
        }
    }

    /// يغلق البحث عند فقدان التركيز إذا كان فارغًا، بعد مهلة تسمح بالضغط على الاقتراحات
    private async void OnInputLostFocus(object? sender, RoutedEventArgs e)
    {
        var token = ++_blurToken;
        await Task.Delay(260);

        if (token == _blurToken && _vm is { IsSearchOpen: true } && !_vm.HasSearchText)
            _vm.IsSearchOpen = false;
    }

    private async Task ShakeIconAsync()
    {
        if (!MotionSettings.Enabled) return;

        if (_icon.RenderTransform is not TransformGroup)
            _icon.RenderTransform = new TransformGroup
            {
                Children = { new TranslateTransform(), new RotateTransform() }
            };

        await new Animation
        {
            Duration = TimeSpan.FromSeconds(0.9),
            Easing = new CubicEaseOut(),
            Children =
            {
                Frame(0, 0, 0), Frame(0.12, 6, 17), Frame(0.26, -5, -14), Frame(0.40, 4, 10),
                Frame(0.55, -3, -6), Frame(0.70, 2, 3), Frame(1, 0, 0),
            }
        }.RunAsync(_icon);
    }

    private static KeyFrame Frame(double cue, double x, double angle) => new()
    {
        Cue = new Cue(cue),
        Setters =
        {
            new Setter(TranslateTransform.XProperty, x),
            new Setter(RotateTransform.AngleProperty, angle),
        }
    };
}
