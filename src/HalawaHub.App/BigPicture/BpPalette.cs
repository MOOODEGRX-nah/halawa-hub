using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture;

/// <summary>
/// تحديث التصميم 6.4: الشاشة الكبيرة تتبع لون التمييز (أحمر، أزرق، بنفسجي، أخضر، برتقالي) وثيم OLED الأسود
/// من الإعدادات › المظهر. تبقى داكنة حتى مع الثيم الفاتح (تلفاز)، والأخطاء تبقى حمراء.
/// ألوان النافذة الثابتة صارت موارد (BpC_ فرشاة، BpK_ لون، BpS_ ظل)، وهنا تُبنى بالألوان الحالية.
/// </summary>
public static class BpPalette
{
    /// درجات الأحمر الأصلية (.bp: --a0 … --a4)
    private static readonly string[] RedRgb = { "DC2626", "EF4444", "B91C1C", "991B1B", "7F1D1D" };

    /// --a4 لكل لون (الدرجات الأربع الأخرى من ThemeManager.AccentPalettes)
    private static readonly Dictionary<string, string> Deepest = new()
    {
        ["red"] = "#7F1D1D", ["blue"] = "#1E3A8A", ["purple"] = "#581C87", ["green"] = "#0F3D22", ["orange"] = "#6C2710",
    };

    /// الرماديات المحمرّة ومقابلها في OLED (.bp.th-oled)
    private static readonly Dictionary<string, string> OledNeutral = new()
    {
        ["0A0404"] = "000000", ["140808"] = "0A0A0A", ["0F0607"] = "050505", ["241010"] = "1A1A1A", ["2A1212"] = "1F1F1F",
        ["2E1616"] = "262626", ["231010"] = "1A1A1A", ["2A1313"] = "1A1A1A", ["0E0606"] = "080808", ["3A1518"] = "2A2A2A",
        ["4A1A1E"] = "2E2E2E", ["060202"] = "000000", ["180909"] = "111111", ["1C0909"] = "141414", ["1A0A0C"] = "111111",
        ["2A1418"] = "1F1F1F", ["F5EDED"] = "F2F2F2", ["B8A4A4"] = "B0B0B0", ["D9C7C7"] = "D4D4D4", ["9C8A8A"] = "9A9A9A",
        ["E8D8D8"] = "E5E5E5", ["140809"] = "0A0A0A",
    };

    public static string Accent { get; private set; } = "red";
    public static bool Oled { get; private set; }
    private static Dictionary<string, string> _accentMap = new();

    /// لون التصميم الأحمر/الداكن → لونه الحالي (الشفافية كما هي)
    public static Color Map(Color c)
    {
        var rgb = $"{c.R:X2}{c.G:X2}{c.B:X2}";
        if (_accentMap.TryGetValue(rgb, out var a)) return WithRgb(c.A, a);
        if (Oled && OledNeutral.TryGetValue(rgb, out var n)) return WithRgb(c.A, n);
        return c;
    }

    public static Color Map(string hex) => Map(Color.Parse(hex));
    public static IBrush Brush(string hex) => new SolidColorBrush(Map(hex));

    private static Color WithRgb(byte alpha, string rgb) =>
        Color.FromArgb(alpha, byte.Parse(rgb[..2], NumberStyles.HexNumber), byte.Parse(rgb[2..4], NumberStyles.HexNumber), byte.Parse(rgb[4..], NumberStyles.HexNumber));

    private static string Hex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    /// الألوان الحالية: لون التمييز من الإعدادات، وOLED إن كان الثيم أسود (الفاتح يبقى داكنًا هنا)
    public static void Use(string? accent, string? theme)
    {
        Accent = ThemeManager.NormalizeAccent(accent);
        Oled = ThemeManager.Normalize(theme) == ThemeManager.Oled;
        var a = ThemeManager.AccentFor(Accent);
        string[] to = { a.Base, a.Hover, a.Deep, a.Darkest, Deepest[Accent] };
        _accentMap = new Dictionary<string, string>();
        for (var i = 0; i < RedRgb.Length; i++) _accentMap[RedRgb[i]] = to[i].TrimStart('#').ToUpperInvariant();
    }

    /// يملأ موارد النافذة بالألوان الحالية
    public static void Apply(IResourceDictionary r)
    {
        // الدرجات الخمس كاملة الإعتام متاحة دائمًا (BpC_FFEF4444 …) حتى لو لم يستعملها ملف الواجهة حرفيًا
        foreach (var hex in System.Linq.Enumerable.Union(BpPaletteKeys.Colors, System.Linq.Enumerable.Select(RedRgb, h => "FF" + h)))
        {
            var c = Map(Color.Parse("#" + hex));
            r["BpC_" + hex] = new SolidColorBrush(c);
            r["BpK_" + hex] = c;
        }
        for (var i = 0; i < BpPaletteKeys.Shadows.Length; i++)
            r["BpS_" + i] = BoxShadows.Parse(Recolor(BpPaletteKeys.Shadows[i]));

        r["BpText"] = Brush("#F5EDED");
        r["BpMuted"] = Brush("#B8A4A4");
        r["BpSurface"] = Brush("#140808");
        r["BpLine"] = Brush("#2A1212");
        r["BpGlassBar"] = Brush("#BF140809");
        r["BpGlassHint"] = Brush("#CC140809");
        r["BpGlassPanel"] = Brush("#EB140809");
        r["BpRedGrad"] = Gradient(0, 0.5, 1, 0.5, "#991B1B", "#DC2626");
        r["BpRedHot"] = Gradient(0, 0, 1, 1, "#991B1B", "#EF4444");
    }

    /// كل لون داخل نص (ظل) بلونه الحالي
    public static string Recolor(string text) =>
        Regex.Replace(text, "#[0-9A-Fa-f]{6,8}", m => Hex(Map(Color.Parse(m.Value))));

    private static LinearGradientBrush Gradient(double x1, double y1, double x2, double y2, string from, string to) => new()
    {
        StartPoint = new Avalonia.RelativePoint(x1, y1, Avalonia.RelativeUnit.Relative),
        EndPoint = new Avalonia.RelativePoint(x2, y2, Avalonia.RelativeUnit.Relative),
        GradientStops = { new GradientStop(Map(from), 0), new GradientStop(Map(to), 1) },
    };
}
