using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace HalawaHub.App.Converters;

/// <summary>يطابق قيمة عددية أو enum مع معامل نصي: Classes.on للقسم المختار.</summary>
public class EqualsTextConverter : IValueConverter
{
    public static readonly EqualsTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// تدرج البطاقة بدون غلاف. ألوان التصميم الستة، ولكل لعبة لون ثابت حسب رقمها.
/// linear-gradient(160deg, a, b) على مستطيل 2:3.
/// </summary>
public class CoverPaletteConverter : IValueConverter
{
    public static readonly CoverPaletteConverter Instance = new();

    private static readonly (string From, string To)[] Palette =
    {
        ("#9F1239", "#1A0508"),
        ("#7C2D12", "#150704"),
        ("#831843", "#14060D"),
        ("#78350F", "#140A03"),
        ("#7F1D1D", "#0F0404"),
        ("#6B1F3A", "#12060B"),
    };

    private static readonly IBrush[] Brushes = BuildBrushes();

    public static int PaletteSize => Palette.Length;

    private static IBrush[] BuildBrushes()
    {
        var result = new IBrush[Palette.Length];
        for (var i = 0; i < Palette.Length; i++)
        {
            result[i] = new ImmutableLinearGradientBrush(
                new List<ImmutableGradientStop>
                {
                    new(0, Color.Parse(Palette[i].From)),
                    new(1, Color.Parse(Palette[i].To)),
                },
                startPoint: new Avalonia.RelativePoint(0.20, -0.048, Avalonia.RelativeUnit.Relative),
                endPoint: new Avalonia.RelativePoint(0.80, 1.048, Avalonia.RelativeUnit.Relative));
        }
        return result;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Brushes[value is int i ? Math.Abs(i) % Brushes.Length : 0];

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>لون القلب في قائمة البطاقة: أحمر مملوء للمفضلة، وإلا بدون تعبئة.</summary>
public class HeartFillConverter : IValueConverter
{
    public static readonly HeartFillConverter Instance = new();
    private static readonly IBrush Red = new ImmutableSolidColorBrush(Color.Parse("#EF4444"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Red : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class HeartStrokeConverter : IValueConverter
{
    public static readonly HeartStrokeConverter Instance = new();
    private static readonly IBrush Red = new ImmutableSolidColorBrush(Color.Parse("#EF4444"));
    private static readonly IBrush Normal = new ImmutableSolidColorBrush(Color.Parse("#F5EDED"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Red : Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
