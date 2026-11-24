using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture.Controls;

/// <summary>
/// مفتاح التشغيل السائل (.sw في التصميم، من toggle.html): مقبض كبير وقطرتان تلحقانه
/// بتأخير، والخلفية الحمراء تظهر بتلاشٍ مع توهج تحتها.
/// </summary>
public sealed class BpSwitch : Panel
{
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<BpSwitch, bool>(nameof(IsOn));

    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    private readonly Border _glow;
    private readonly Border _fill;
    private readonly Ellipse[] _knobs;
    // (القطر، البداية مطفأ، البداية مشغّل، التأخير)
    private static readonly (double Size, double Off, double On, double Delay)[] Knobs =
    {
        (14, 15, 55, 0.095), (22, 11, 51, 0.045), (34, 5, 45, 0),
    };

    public BpSwitch()
    {
        Width = 84;
        Height = 44;
        var grad = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = { new GradientStop(BpPalette.Map("#991B1B"), 0), new GradientStop(BpPalette.Map("#EF4444"), 1) },
        };
        _glow = new Border { CornerRadius = new CornerRadius(22), Background = grad, Opacity = 0, Effect = new BlurEffect { Radius = 24 } };
        var track = new Border { CornerRadius = new CornerRadius(22), Background = BpPalette.Brush("#2A1313"), BorderBrush = new SolidColorBrush(Color.Parse("#14FFFFFF")), BorderThickness = new Thickness(1) };
        _fill = new Border { CornerRadius = new CornerRadius(22), Background = grad, Opacity = 0 };
        Children.Add(_glow);
        Children.Add(track);
        Children.Add(_fill);

        var knobBrush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Color.Parse("#E9DADA"), 1) },
        };
        _knobs = new Ellipse[Knobs.Length];
        for (var i = 0; i < Knobs.Length; i++)
        {
            var k = Knobs[i];
            var e = new Ellipse
            {
                Width = k.Size, Height = k.Size, Fill = knobBrush,
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(k.Off, (44 - k.Size) / 2, 0, 0),
                RenderTransformOrigin = RelativePoint.Center,
            };
            if (i == Knobs.Length - 1) e.Effect = new DropShadowEffect { OffsetY = 3, BlurRadius = 8, Opacity = 0.35, Color = Colors.Black };
            _knobs[i] = e;
            Children.Add(e);
        }
        ApplyTransitions();
    }

    private void ApplyTransitions()
    {
        if (!MotionSettings.Enabled)
        {
            foreach (var k in _knobs) k.Transitions = null;
            _fill.Transitions = _glow.Transitions = null;
            return;
        }
        for (var i = 0; i < _knobs.Length; i++)
            _knobs[i].Transitions = new Transitions
            {
                new ThicknessTransition { Property = MarginProperty, Duration = TimeSpan.FromSeconds(0.55), Delay = TimeSpan.FromSeconds(Knobs[i].Delay), Easing = BpSprings.Knob },
            };
        _fill.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromSeconds(0.3) } };
        _glow.Transitions = new Transitions { new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromSeconds(0.35) } };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsOnProperty) return;
        var on = change.GetNewValue<bool>();
        _fill.Opacity = on ? 1 : 0;
        _glow.Opacity = on ? 0.75 : 0;
        for (var i = 0; i < _knobs.Length; i++)
        {
            var k = Knobs[i];
            _knobs[i].Margin = new Thickness(on ? k.On : k.Off, (44 - k.Size) / 2, 0, 0);
        }
        if (MotionSettings.Enabled && Avalonia.VisualTree.VisualExtensions.GetVisualRoot(this) != null) _ = Stretch(_knobs[^1]);
    }

    /// kStrA: المقبض الكبير يتمدد أفقيًا أثناء الانتقال ثم يعود
    private static async System.Threading.Tasks.Task Stretch(Ellipse knob)
    {
        var scale = new ScaleTransform();
        knob.RenderTransform = scale;
        var anim = new Avalonia.Animation.Animation
        {
            Duration = TimeSpan.FromSeconds(0.5),
            Easing = BpSprings.Knob,
            Children =
            {
                Frame(0, 1, 1), Frame(0.3, 1.32, 0.84), Frame(0.7, 0.94, 1.05), Frame(1, 1, 1),
            },
        };
        try { await anim.RunAsync(knob); } catch (Exception ex) { HalawaHub.Core.Log.Error("switch stretch failed", ex); }
    }

    private static KeyFrame Frame(double cue, double sx, double sy) => new()
    {
        Cue = new Cue(cue),
        Setters = { new Avalonia.Styling.Setter(ScaleTransform.ScaleXProperty, sx), new Avalonia.Styling.Setter(ScaleTransform.ScaleYProperty, sy) },
    };
}
