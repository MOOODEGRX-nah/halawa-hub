using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HalawaHub.App.Converters;

/// يحوّل اسم المنصة للونها المميز (زي شعارات Steam/GOG/Epic الرسمية تقريبًا)
public class PlatformColorConverter : IValueConverter
{
    public static readonly PlatformColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var platform = value as string ?? "";
        return new SolidColorBrush(Color.Parse(platform switch
        {
            "Steam" => "#66C0F4",
            "GOG" => "#A855F7",
            "Epic Games" => "#B0B0B0",
            "Riot Games" => "#EF4444",
            "Xbox / Microsoft Store" => "#22C55E",
            "Heroic" => "#38BDF8",
            "Lutris" => "#F97316",
            _ => "#777777"
        }));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// يحوّل حالة "مثبتة/غير مثبتة" لنقطة ملوّنة صغيرة بجانب اسم اللعبة
public class BoolToStatusColorConverter : IValueConverter
{
    public static readonly BoolToStatusColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isInstalled = value is true;
        return new SolidColorBrush(Color.Parse(isInstalled ? "#4CAF50" : "#666666"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// يقارن قيمة نصية بمعامل ثابت — يُستخدم لربط أزرار الشريط الجانبي
/// (RadioButton) بالعنصر المختار حاليًا، ويدعم الاتجاهين (Two-Way)
public class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) == (parameter as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? parameter as string : Avalonia.Data.BindingOperations.DoNothing;
}

/// نبرة حالة اللعبة (GameCardViewModel.StatusTone) → لونها من الثيم
public class StatusToneBrushConverter : IValueConverter
{
    public static readonly StatusToneBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is int tone
            ? tone switch
            {
                0 => "StatusOfflineBrush",
                1 => "StatusRunningBrush",
                2 => "StatusRepairBrush",
                3 => "StatusUpdateBrush",
                _ => "TextBrush"
            }
            : "TextBrush";
        return Avalonia.Application.Current?.TryGetResource(key, null, out var brush) == true ? brush : Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// مفتاح أيقونة مجموعة ("star") → شكلها من موارد البرنامج (IconStar)
public class IconKeyConverter : IValueConverter
{
    public static readonly IconKeyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrEmpty(key)) key = "star";
        var name = "Icon" + char.ToUpperInvariant(key[0]) + key[1..];
        var app = Avalonia.Application.Current;
        if (app != null && app.TryGetResource(name, null, out var g) && g != null) return g;
        return app != null && app.TryGetResource("IconStar", null, out var star) ? star : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
