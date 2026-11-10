using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;

namespace HalawaHub.App.Views;

public partial class LibraryView : UserControl
{
    private const int MaxStaggeredCards = 24;
    private static readonly TimeSpan PageSlide = TimeSpan.FromSeconds(0.6);
    private static readonly Easing PageEasing = new CssBezierEasing(0.22, 1.18, 0.36, 1);

    private MainViewModel? _vm;
    private readonly ScrollViewer _scroll;
    private readonly Image _snapshot;
    private readonly VectorIcon _refreshIcon;
    private bool _spinning;

    public LibraryView()
    {
        InitializeComponent();

        _scroll = this.FindControl<ScrollViewer>("PageScroll")!;
        _snapshot = this.FindControl<Image>("PageSnapshot")!;
        _refreshIcon = this.FindControl<VectorIcon>("RefreshIcon")!;

        this.FindControl<ItemsControl>("GamesList")!.ContainerPrepared += OnCardPrepared;

        DataContextChanged += (_, _) =>
        {
            if (_vm != null) _vm.PropertyChanged -= OnViewModelChanged;
            _vm = DataContext as MainViewModel;
            if (_vm != null) _vm.PropertyChanged += OnViewModelChanged;
            UpdateRefreshSpin();
        };
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRefreshing))
            UpdateRefreshSpin();
    }

    /// البطاقات تدخل واحدة تلو الأخرى عند تغيّر الصفحة أو الفلتر
    private void OnCardPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (!MotionSettings.Enabled) return;

        var delay = TimeSpan.FromMilliseconds(Math.Min(e.Index, MaxStaggeredCards) * 45);
        _ = MotionSettings.AnimateAsync(e.Container, TimeSpan.FromSeconds(0.5), MotionSettings.EnterEasing,
            0, 0.97, 18, delay: delay);
    }

    private void Tab_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not Button { Tag: string tag }) return;

        var index = int.Parse(tag);
        var current = _vm.LibraryTabIndex;
        if (index == current) return;

        var snapshot = CaptureCurrentPage();
        _vm.SelectLibraryTab(index);
        _scroll.Offset = default;

        _ = SlidePagesAsync(snapshot, forward: index > current);
    }

    private RenderTargetBitmap? CaptureCurrentPage()
    {
        if (!MotionSettings.Enabled || _scroll.Bounds.Width <= 0 || _scroll.Bounds.Height <= 0) return null;

        try
        {
            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var pixels = new PixelSize((int)(_scroll.Bounds.Width * scaling), (int)(_scroll.Bounds.Height * scaling));
            var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * scaling, 96 * scaling));
            bitmap.Render(_scroll);
            return bitmap;
        }
        catch (Exception ex)
        {
            HalawaHub.Core.Log.Error("could not capture the page for the slide animation", ex);
            return null;
        }
    }

    /// الصفحة الجديدة تنزلق مكان لقطة الصفحة القديمة. لقطة واحدة أخف بكثير من
    /// إبقاء أربع شبكات بطاقات حية طوال الوقت.
    private async Task SlidePagesAsync(RenderTargetBitmap? snapshot, bool forward)
    {
        if (snapshot == null) return;

        var width = _scroll.Bounds.Width;
        var rtl = Loc.Instance.IsRtl;

        // الانتقال للأمام يدفع الصفحة نحو بداية القراءة
        var sign = forward != rtl ? -1 : 1;

        _snapshot.Source = snapshot;
        _snapshot.IsVisible = true;
        _snapshot.RenderTransform = new TranslateTransform();
        _scroll.RenderTransform = new TranslateTransform();

        // Scroll يعيش داخل الاتجاه المعكوس في العربية، فنعوّض الإشارة
        var scrollSign = rtl ? -sign : sign;

        await Task.WhenAll(
            Slide(_snapshot, 0, sign * width),
            Slide(_scroll, -scrollSign * width, 0));

        _snapshot.IsVisible = false;
        _snapshot.Source = null;
        snapshot.Dispose();
        _scroll.RenderTransform = null;
    }

    private static Task Slide(Visual target, double from, double to) => new Animation
    {
        Duration = PageSlide,
        Easing = PageEasing,
        FillMode = FillMode.Both,
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, from) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, to) } },
        }
    }.RunAsync(target);

    private async void AddGame_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        var file = await GamePickers.PickGameFileAsync(this);
        if (file != null) await _vm.AddManualGameAsync(file);
    }

    private void Sort_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string mode }) _vm?.SetSortMode(mode);
    }

    private async void UpdateRefreshSpin()
    {
        if (_vm is not { IsRefreshing: true } || _spinning || !MotionSettings.Enabled) return;

        _spinning = true;
        _refreshIcon.RenderTransform = new RotateTransform();

        var spin = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.7),
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(RotateTransform.AngleProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(RotateTransform.AngleProperty, 360d) } },
            }
        };

        while (_vm is { IsRefreshing: true })
            await spin.RunAsync(_refreshIcon);

        _spinning = false;
    }
}
