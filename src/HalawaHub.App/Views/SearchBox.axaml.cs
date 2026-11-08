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
using Avalonia.Media.Transformation;
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

    private static readonly Easing Spring = new CssBezierEasing(0.34, 1.56, 0.64, 1);

    private MainViewModel? _vm;
    private readonly Panel _box;
    private readonly Border _fill;
    private readonly Border _shell;
    private readonly TextBox _input;
    private readonly VectorIcon _icon;
    private readonly Control _clear;
    private readonly Control _bubble;
    private readonly Control _dropdown;
    private readonly Control _noMatch;
    private int _blurToken;
    private bool _wasClearVisible;
    private bool _wasBubbleVisible;
    private bool _wasDropdownVisible;

    public SearchBox()
    {
        InitializeComponent();

        _box = this.FindControl<Panel>("Box")!;
        _fill = this.FindControl<Border>("Fill")!;
        _shell = this.FindControl<Border>("Shell")!;
        _input = this.FindControl<TextBox>("Input")!;
        _icon = this.FindControl<VectorIcon>("SearchIcon")!;
        _clear = this.FindControl<Control>("ClearButton")!;
        _bubble = this.FindControl<Control>("Bubble")!;
        _dropdown = this.FindControl<Control>("Dropdown")!;
        _noMatch = this.FindControl<Control>("NoMatchPanel")!;

        _box.RenderTransform = TransformOperations.Parse("scale(1)");
        _box.Transitions = BoxTransitions(open: false);

        _input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        _input.LostFocus += OnInputLostFocus;
        _input.GotFocus += (_, _) => _blurToken++;

        // ضغطة الزر المغلق تنكمش بنابض (.sb:not(.open):active)
        var open = this.FindControl<Button>("OpenButton")!;
        open.AddHandler(PointerPressedEvent, (_, _) => SetPressed(true), RoutingStrategies.Tunnel);
        open.AddHandler(PointerReleasedEvent, (_, _) => SetPressed(false), RoutingStrategies.Tunnel);
        open.AddHandler(PointerCaptureLostEvent, (_, _) => SetPressed(false), RoutingStrategies.Tunnel);

        // العناصر تظهر تباعًا (sbFade و sbRow)
        this.FindControl<ItemsControl>("RecentList")!.ContainerPrepared += (_, e) =>
            _ = MotionSettings.AnimateAsync(e.Container, TimeSpan.FromSeconds(0.3), new CubicEaseOut(), 0, 1, 0);
        this.FindControl<ItemsControl>("SuggestionList")!.ContainerPrepared += (_, e) =>
            _ = MotionSettings.AnimateAsync(e.Container, TimeSpan.FromSeconds(0.38), Spring, 0, 1, -18,
                delay: TimeSpan.FromMilliseconds(50 + 80 * e.Index));

        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnViewModelChanged;
            _vm = DataContext as MainViewModel;
            if (_vm != null) _vm.PropertyChanged += OnViewModelChanged;
        };
    }

    private static Transitions? BoxTransitions(bool open)
    {
        if (!MotionSettings.Enabled) return null;

        // فتح بنابض، وإغلاق أهدأ
        return new Transitions
        {
            new DoubleTransition
            {
                Property = WidthProperty,
                Duration = TimeSpan.FromSeconds(open ? 0.42 : 0.35),
                Easing = open ? new CssBezierEasing(0.34, 1.56, 0.64, 1) : new CssBezierEasing(0.65, 0, 0.35, 1)
            },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromSeconds(0.3),
                Easing = new CssBezierEasing(0.3, 1.8, 0.5, 1)
            }
        };
    }

    private void SetPressed(bool pressed) =>
        _box.RenderTransform = TransformOperations.Parse(pressed ? "scale(0.88)" : "scale(1)");

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        var vm = _vm!;

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsSearchOpen):
                ApplyOpenState(vm.IsSearchOpen);
                break;

            case nameof(MainViewModel.HasSearchText):
                if (vm.HasSearchText && !_wasClearVisible)
                    _ = MotionSettings.AnimateAsync(_clear, TimeSpan.FromSeconds(0.15), new LinearEasing(), 0, 1, 0);
                _wasClearVisible = vm.HasSearchText;
                break;

            case nameof(MainViewModel.ShowSearchDropdown):
                if (vm.ShowSearchDropdown && !_wasDropdownVisible)
                    _ = MotionSettings.AnimateAsync(_dropdown, TimeSpan.FromSeconds(0.25), new CubicEaseOut(), 0, 1, -6);
                _wasDropdownVisible = vm.ShowSearchDropdown;
                break;

            case nameof(MainViewModel.ShowSearchNoMatch):
                if (vm.ShowSearchNoMatch && !_wasBubbleVisible)
                {
                    _ = MotionSettings.AnimateAsync(_bubble, TimeSpan.FromSeconds(0.4), Spring, 1, 0, 0,
                        delay: TimeSpan.FromSeconds(0.12));
                    _ = MotionSettings.AnimateAsync(_noMatch, TimeSpan.FromSeconds(0.3), new CubicEaseOut(), 0, 1, 0,
                        delay: TimeSpan.FromSeconds(0.2));
                }
                _wasBubbleVisible = vm.ShowSearchNoMatch;
                break;

            case nameof(MainViewModel.SearchNoMatchText):
                if (vm.ShowSearchNoMatch) _ = ShakeIconAsync();
                break;
        }
    }

    private void ApplyOpenState(bool open)
    {
        _box.Transitions = BoxTransitions(open);

        _box.Width = open ? OpenWidth : ClosedWidth;
        _fill.Opacity = open ? 0 : 1;
        _shell.Opacity = open ? 1 : 0;
        _icon.Foreground = open ? this.FindResource("MutedBrush") as IBrush : Brushes.White;

        if (open)
        {
            // الحقل يظهر بعد بدء التوسع
            _ = MotionSettings.AnimateAsync(_input, TimeSpan.FromSeconds(0.25), new LinearEasing(), 0, 1, 0,
                delay: TimeSpan.FromSeconds(0.28));
            Dispatcher.UIThread.Post(() => _input.Focus(), DispatcherPriority.Background);
        }
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
