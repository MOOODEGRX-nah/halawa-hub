using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace HalawaHub.App.BigPicture.Controls;

/// <summary>مفتاح المفتاح القابل للتركيز، ونصف قطر الحلقة حوله.</summary>
public static class BpFocus
{
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Key", typeof(BpFocus));
    public static readonly AttachedProperty<double> RadiusProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("Radius", typeof(BpFocus), 14);

    public static string? GetKey(Control c) => c.GetValue(KeyProperty);
    public static void SetKey(Control c, string? v) => c.SetValue(KeyProperty, v);
    public static double GetRadius(Control c) => c.GetValue(RadiusProperty);
    public static void SetRadius(Control c, double v) => c.SetValue(RadiusProperty, v);
}

/// <summary>
/// رمز زر يد التحكم كما في التصميم (.gl): دائرة بإطار بلون الزر، ورموز
/// PlayStation (✕ ○ □ △) أو Xbox (A B X Y)، أو مستطيل LB/RB، أو ≡ للقائمة.
/// </summary>
public sealed class BpGlyph : Control
{
    public static readonly StyledProperty<string> ButtonProperty =
        AvaloniaProperty.Register<BpGlyph, string>(nameof(Button), "a");
    public static readonly StyledProperty<bool> PsProperty =
        AvaloniaProperty.Register<BpGlyph, bool>(nameof(Ps));
    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<BpGlyph, double>(nameof(Size), 40);

    static BpGlyph()
    {
        AffectsRender<BpGlyph>(ButtonProperty, PsProperty, SizeProperty);
        AffectsMeasure<BpGlyph>(ButtonProperty, PsProperty, SizeProperty);
    }

    public string Button { get => GetValue(ButtonProperty); set => SetValue(ButtonProperty, value); }
    public bool Ps { get => GetValue(PsProperty); set => SetValue(PsProperty, value); }
    public double Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    private static IBrush Ground => BpPalette.Brush("#140808");
    private static readonly Typeface Face = new("fonts:Inter#Inter", FontStyle.Normal, FontWeight.ExtraBold);

    /// الرسم المباشر لا يُعكس في العربية: اتجاه ثابت يلغي مرآة الأب
    public BpGlyph() => FlowDirection = FlowDirection.LeftToRight;

    private bool IsBumper => Button is "lb" or "rb";

    private string Label => Button switch
    {
        "lb" => Ps ? "L1" : "LB",
        "rb" => Ps ? "R1" : "RB",
        "menu" => "≡",
        "a" => Ps ? "✕" : "A",
        "b" => Ps ? "" : "B",
        "x" => Ps ? "" : "X",
        "y" => Ps ? "△" : "Y",
        _ => "",
    };

    private Color Tint => (Button, Ps) switch
    {
        ("a", false) => Color.Parse("#4ADE80"),
        ("a", true) => Color.Parse("#60A5FA"),
        ("b", _) => Color.Parse("#F87171"),
        ("x", false) => Color.Parse("#60A5FA"),
        ("x", true) => Color.Parse("#F472B6"),
        ("y", false) => Color.Parse("#FACC15"),
        ("y", true) => Color.Parse("#34D399"),
        _ => Color.Parse("#D9C7C7"),
    };

    protected override Size MeasureOverride(Size availableSize) =>
        IsBumper ? new Size(Size * 1.35, Size) : new Size(Size, Size);

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var brush = new SolidColorBrush(Tint);
        var pen = new Pen(brush, Size >= 36 ? 2 : 1.5);
        var rect = new Rect(1, 1, w - 2, h - 2);

        if (IsBumper) context.DrawRectangle(Ground, pen, rect, 12 * Size / 40, 12 * Size / 40);
        else context.DrawEllipse(Ground, pen, rect.Center, rect.Width / 2, rect.Height / 2);

        var c = new Point(w / 2, h / 2);
        var shapePen = new Pen(brush, 2.5 * Size / 40);
        if (Ps && Button == "b")
        {
            var r = 7.5 * Size / 40;
            context.DrawEllipse(null, shapePen, c, r, r);
            return;
        }
        if (Ps && Button == "x")
        {
            var s = 13 * Size / 40;
            context.DrawRectangle(null, shapePen, new Rect(c.X - s / 2, c.Y - s / 2, s, s), 2, 2);
            return;
        }

        var fontSize = (IsBumper ? 15 : Button == "a" && Ps ? 20 : Button == "y" && Ps ? 17 : 18) * Size / 40;
        var text = new FormattedText(Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, fontSize, brush);
        context.DrawText(text, new Point(c.X - text.Width / 2, c.Y - text.Height / 2));
    }
}
