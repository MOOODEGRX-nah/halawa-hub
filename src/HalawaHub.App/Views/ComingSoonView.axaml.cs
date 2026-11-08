using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using HalawaHub.App.Services;

namespace HalawaHub.App.Views;

public partial class ComingSoonView : UserControl
{
    private const double FillRatio = 0.45;

    private readonly Border _bar;
    private readonly Border _fill;
    private System.Threading.CancellationTokenSource? _loop;

    public ComingSoonView()
    {
        InitializeComponent();

        _bar = this.FindControl<Border>("Bar")!;
        _fill = this.FindControl<Border>("BarFill")!;

        _fill.RenderTransform = new TranslateTransform();
        _bar.SizeChanged += (_, _) => Restart();
        this.GetObservable(IsVisibleProperty).Subscribe(new VisibilityObserver(Restart));
    }

    /// شريط غير محدد: الجزء الأحمر يعبر الشريط باستمرار ما دامت الصفحة ظاهرة
    private void Restart()
    {
        _loop?.Cancel();
        _loop = null;

        var width = _bar.Bounds.Width;
        _fill.Width = width * FillRatio;

        if (!MotionSettings.Enabled || !IsEffectivelyVisible || width <= 0) return;

        _loop = new System.Threading.CancellationTokenSource();

        new Animation
        {
            Duration = TimeSpan.FromSeconds(1.6),
            IterationCount = IterationCount.Infinite,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, -width * FillRatio) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, width) } },
            }
        }.RunAsync(_fill, _loop.Token);
    }

    private sealed class VisibilityObserver : IObserver<bool>
    {
        private readonly Action _onChange;
        public VisibilityObserver(Action onChange) => _onChange = onChange;
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(bool value) => _onChange();
    }
}
