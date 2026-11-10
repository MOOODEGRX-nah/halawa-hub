using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace HalawaHub.App.Services;

/// <summary>
/// الثيم (داكن، أسود OLED، فاتح) ولون التمييز. كل ألوان الواجهة موارد بأسماء ثابتة
/// تُقرأ بـ DynamicResource، فتغيير الثيم يظهر فورًا بلا إعادة تشغيل. قيم "داكن + أحمر"
/// هي نفس ألوان التصميم الأصلي بالضبط.
///
/// النصوص فوق صور الأغلفة (اسم اللعبة، الشارات) ثابتة اللون في كل الثيمات لأن خلفيتها
/// الصورة نفسها مع تدرّج داكن، وليست موارد هنا.
/// </summary>
public static class ThemeManager
{
    public const string Dark = "dark";
    public const string Oled = "oled";
    public const string Light = "light";

    public static readonly string[] Themes = { Dark, Oled, Light };
    public static readonly string[] Accents = { "red", "blue", "purple", "green", "orange" };

    public static string CurrentTheme { get; private set; } = Dark;
    public static string CurrentAccent { get; private set; } = "red";

    /// القيم الافتراضية للثيم (الداكن الأصلي)
    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["PageBrush"] = "#0A0404", ["SurfaceBrush"] = "#140808", ["PanelBrush"] = "#0F0607", ["DialogBrush"] = "#0E0606",
        ["HoverBrush"] = "#241010", ["BorderBrushCard"] = "#2A1212", ["BorderBrushControl"] = "#2E1616", ["DividerBrush"] = "#231010",
        ["TextBrush"] = "#F5EDED", ["MutedBrush"] = "#B8A4A4", ["ChipTextBrush"] = "#D9C7C7", ["SubtleBrush"] = "#9C8A8A",
        ["MenuTextBrush"] = "#E8D8D8", ["MenuBrush"] = "#1A0A0C", ["MenuBorderBrush"] = "#3A1518", ["MenuHoverBrush"] = "#2A1418",
        ["SelectedRowBrush"] = "#4A1A1E", ["FieldBrush"] = "#1C0E0F", ["FieldFocusBorderBrush"] = "#4A2024",
        ["ScrimBrush"] = "#C7060202", ["ChipBrush"] = "#2A1313",
        ["Wash04Brush"] = "#0AFFFFFF", ["Wash06Brush"] = "#0FFFFFFF", ["Wash07Brush"] = "#12FFFFFF", ["Wash08Brush"] = "#14FFFFFF",
        ["Wash10Brush"] = "#1AFFFFFF", ["Wash25Brush"] = "#40FFFFFF",
        ["AmberBrush"] = "#F5A623", ["StatBadTextBrush"] = "#FCA5A5",
        ["StatusUpdateBrush"] = "#F5A623", ["StatusRepairBrush"] = "#EF4444", ["StatusRunningBrush"] = "#34D399", ["StatusOfflineBrush"] = "#9C8A8A",
        ["StatOkTop"] = "#1C0909", ["StatBadTop"] = "#2B0B0B", ["StatBottom"] = "#0A0404"
    };

    private static readonly Dictionary<string, string> OledPalette = new()
    {
        ["PageBrush"] = "#000000", ["SurfaceBrush"] = "#0A0A0A", ["PanelBrush"] = "#050505", ["DialogBrush"] = "#080808",
        ["HoverBrush"] = "#1A1A1A", ["BorderBrushCard"] = "#1F1F1F", ["BorderBrushControl"] = "#262626", ["DividerBrush"] = "#1A1A1A",
        ["TextBrush"] = "#F2F2F2", ["MutedBrush"] = "#B0B0B0", ["ChipTextBrush"] = "#D4D4D4", ["SubtleBrush"] = "#9A9A9A",
        ["MenuTextBrush"] = "#E5E5E5", ["MenuBrush"] = "#111111", ["MenuBorderBrush"] = "#2A2A2A", ["MenuHoverBrush"] = "#1F1F1F",
        ["SelectedRowBrush"] = "#2E2E2E", ["FieldBrush"] = "#111111", ["FieldFocusBorderBrush"] = "#3A3A3A",
        ["ScrimBrush"] = "#D9000000", ["ChipBrush"] = "#1A1A1A",
        ["Wash04Brush"] = "#0AFFFFFF", ["Wash06Brush"] = "#0FFFFFFF", ["Wash07Brush"] = "#12FFFFFF", ["Wash08Brush"] = "#14FFFFFF",
        ["Wash10Brush"] = "#1AFFFFFF", ["Wash25Brush"] = "#40FFFFFF",
        ["AmberBrush"] = "#F5A623", ["StatBadTextBrush"] = "#FCA5A5",
        ["StatusUpdateBrush"] = "#F5A623", ["StatusRepairBrush"] = "#EF4444", ["StatusRunningBrush"] = "#34D399", ["StatusOfflineBrush"] = "#9A9A9A",
        ["StatOkTop"] = "#141414", ["StatBadTop"] = "#1F0A0A", ["StatBottom"] = "#000000"
    };

    /// <summary>
    /// فاتح: كل نص على خلفيته يحقق تباين WCAG AA (4.5:1 فأكثر) — النسب في
    /// ThemeContrastTests. الكهرماني والأخضر الفاتحان لا يُقرآن على الأبيض فلهما بدائل أغمق.
    /// </summary>
    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["PageBrush"] = "#F6F2F2", ["SurfaceBrush"] = "#FFFFFF", ["PanelBrush"] = "#F0EAEA", ["DialogBrush"] = "#FFFFFF",
        ["HoverBrush"] = "#EFE6E6", ["BorderBrushCard"] = "#E0D4D4", ["BorderBrushControl"] = "#D6C8C8", ["DividerBrush"] = "#E6DCDC",
        ["TextBrush"] = "#1F1414", ["MutedBrush"] = "#5C4A4A", ["ChipTextBrush"] = "#3D2E2E", ["SubtleBrush"] = "#6A5757",
        ["MenuTextBrush"] = "#2B1D1D", ["MenuBrush"] = "#FFFFFF", ["MenuBorderBrush"] = "#E0CFCF", ["MenuHoverBrush"] = "#F4EAEA",
        ["SelectedRowBrush"] = "#F8DADA", ["FieldBrush"] = "#FFFFFF", ["FieldFocusBorderBrush"] = "#C9A9A9",
        ["ScrimBrush"] = "#8C1A0F0F", ["ChipBrush"] = "#F1E6E6",
        ["Wash04Brush"] = "#0A000000", ["Wash06Brush"] = "#0F000000", ["Wash07Brush"] = "#12000000", ["Wash08Brush"] = "#14000000",
        ["Wash10Brush"] = "#1A000000", ["Wash25Brush"] = "#40000000",
        ["AmberBrush"] = "#92400E", ["StatBadTextBrush"] = "#B91C1C",
        ["StatusUpdateBrush"] = "#92400E", ["StatusRepairBrush"] = "#B91C1C", ["StatusRunningBrush"] = "#047857", ["StatusOfflineBrush"] = "#6A5757",
        ["StatOkTop"] = "#FDF0F0", ["StatBadTop"] = "#FDE2E2", ["StatBottom"] = "#FFFFFF"
    };

    /// (أساسي، تمرير، عميق، داكن) — الأساسي يحمل نصًا أبيض بتباين 4.5:1 فأكثر
    private static readonly Dictionary<string, (string Base, string Hover, string Deep, string Darkest)> AccentPalettes = new()
    {
        ["red"] = ("#DC2626", "#EF4444", "#B91C1C", "#991B1B"),
        ["blue"] = ("#2563EB", "#3B82F6", "#1D4ED8", "#1E40AF"),
        ["purple"] = ("#9333EA", "#A855F7", "#7E22CE", "#6B21A8"),
        ["green"] = ("#15803D", "#16A34A", "#166534", "#14532D"),
        ["orange"] = ("#C2410C", "#EA580C", "#9A3412", "#7C2D12")
    };

    public static string Normalize(string? theme) => Array.IndexOf(Themes, theme) >= 0 ? theme! : Dark;
    public static string NormalizeAccent(string? accent) => accent != null && AccentPalettes.ContainsKey(accent) ? accent : "red";

    internal static IReadOnlyDictionary<string, string> PaletteFor(string theme) => Normalize(theme) switch
    {
        Oled => OledPalette,
        Light => LightPalette,
        _ => DarkPalette
    };

    internal static (string Base, string Hover, string Deep, string Darkest) AccentFor(string accent) =>
        AccentPalettes[NormalizeAccent(accent)];

    /// لون نص التمييز (العناوين الحمراء): على الفاتح نستخدم العميق ليُقرأ على الأبيض
    internal static string AccentTextColor(string theme, string accent) =>
        Normalize(theme) == Light ? AccentFor(accent).Deep : AccentFor(accent).Hover;

    public static void Apply(Application app, string? theme, string? accent)
    {
        theme = Normalize(theme);
        accent = NormalizeAccent(accent);
        CurrentTheme = theme;
        CurrentAccent = accent;

        var r = app.Resources;
        var palette = PaletteFor(theme);
        foreach (var (key, hex) in palette)
        {
            if (key.EndsWith("Brush", StringComparison.Ordinal)) r[key] = Brush(hex);
        }

        var a = AccentFor(accent);
        var hover = Color.Parse(a.Hover);
        var deep = Color.Parse(a.Deep);
        var darkest = Color.Parse(a.Darkest);
        var amber = Color.Parse("#F5A623");

        r["RedBrush"] = Brush(a.Base);
        r["RedHoverBrush"] = Brush(AccentTextColor(theme, accent));
        r["AccentHoverFillBrush"] = Brush(a.Hover);
        r["AccentDeepBrush"] = Brush(a.Deep);
        r["RedGradient"] = Gradient(0, 0.5, 1, 0.5, (darkest, 0), (Color.Parse(a.Base), 1));
        r["UnderlineGradient"] = Gradient(0, 0.5, 1, 0.5, (darkest, 0), (hover, 1));
        r["SettingsHoverGradient"] = Gradient(0, 0, 1, 1, (darkest, 0), (hover, 0.6), (amber, 1));
        r["MenuOpenGradient"] = Gradient(0, 0, 1, 1, (darkest, 0), (hover, 1));
        r["AccentAmberGradient"] = Gradient(0, 0.5, 1, 0.5, (deep, 0), (amber, 1));
        r["AccentHoverAmberGradient"] = Gradient(0, 0, 1, 1, (hover, 0), (amber, 1));
        r["ComingSoonGradient"] = Gradient(0, 0.5, 1, 0.5, (deep, 0), (hover, 1));
        r["WindowGlowBrush"] = Glow(deep, 0x38, -0.1, 900, 400);
        r["DialogGlowBrush"] = Glow(deep, 0x2E, -0.2, 700, 300);

        r["StatOkBorderBrush"] = new SolidColorBrush(WithAlpha(hover, 0x59));
        r["StatBadBorderBrush"] = new SolidColorBrush(WithAlpha(Color.Parse(a.Base), 0x8C));
        r["SearchHaloBrush"] = new SolidColorBrush(WithAlpha(hover, 0x99));
        r["StatOkBackground"] = Gradient(0, 0.5, 1, 0.5, (Color.Parse(palette["StatOkTop"]), 0), (Color.Parse(palette["StatBottom"]), 0.6));
        r["StatBadBackground"] = Gradient(0, 0.5, 1, 0.5, (Color.Parse(palette["StatBadTop"]), 0), (Color.Parse(palette["StatBottom"]), 0.6));

        // توهج التمييز: نفس المقاسات والشفافيات الأصلية، بلون التمييز المختار
        r["GlowM"] = Shadow(0, 0, 16, hover, 0x73);
        r["GlowS"] = Shadow(0, 0, 12, hover, 0x73);
        r["GlowUnderline"] = Shadow(0, 0, 12, hover, 0xB3);
        r["GlowChip"] = Shadow(0, 0, 16, hover, 0x80);
        r["GlowSearch"] = Shadow(0, 0, 16, hover, 0x8C);
        r["GlowSearchS"] = Shadow(0, 0, 12, hover, 0x80);
        r["GlowLogo"] = Shadow(0, 0, 12, hover, 0x4D);
        r["GlowTop"] = Shadow(0, 0, 18, hover, 0x47);
        r["GlowTopOn"] = Shadow(0, 0, 20, hover, 0x8C);
        r["GlowTopHover"] = Shadow(0, 0, 18, hover, 0x80);
        r["GlowCardHover"] = Shadow(0, 12, 32, hover, 0x47);
        r["GlowMenuOpen"] = Shadow(0, 0, 18, hover, 0x99);
        r["GlowCoverHover"] = Shadow(0, 14, 30, hover, 0x4D);

        app.RequestedThemeVariant = theme == Light ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));

    private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    private static BoxShadows Shadow(double x, double y, double blur, Color c, byte alpha) =>
        new(new BoxShadow { OffsetX = x, OffsetY = y, Blur = blur, Color = WithAlpha(c, alpha) });

    private static RadialGradientBrush Glow(Color c, byte alpha, double centerY, double rx, double ry) => new()
    {
        Center = new RelativePoint(0.85, centerY, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.85, centerY, RelativeUnit.Relative),
        RadiusX = new RelativeScalar(rx, RelativeUnit.Absolute),
        RadiusY = new RelativeScalar(ry, RelativeUnit.Absolute),
        GradientStops = { new GradientStop(WithAlpha(c, alpha), 0), new GradientStop(WithAlpha(c, 0), 1) }
    };

    private static LinearGradientBrush Gradient(double x1, double y1, double x2, double y2, params (Color Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(x1, y1, RelativeUnit.Relative),
            EndPoint = new RelativePoint(x2, y2, RelativeUnit.Relative)
        };
        foreach (var (color, offset) in stops) brush.GradientStops.Add(new GradientStop(color, offset));
        return brush;
    }

    /// تباين WCAG بين لونين (للاختبارات ولتوثيق الثيم الفاتح)
    public static double Contrast(string fg, string bg)
    {
        static double L(Color c)
        {
            static double Ch(byte v)
            {
                var s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }
        var a = L(Color.Parse(fg));
        var b = L(Color.Parse(bg));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
}
