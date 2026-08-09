using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RiftClone.App.Converters;

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
