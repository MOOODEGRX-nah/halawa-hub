using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using HalawaHub.App.Services;

namespace HalawaHub.App.BigPicture;

/// <summary>محوّلات صغيرة لواجهة الشاشة الكبيرة.</summary>
public static class BpIs
{
    private static readonly IBrush BarOn = new SolidColorBrush(Color.Parse("#4ADE80"));
    private static readonly IBrush BarOff = new SolidColorBrush(Color.Parse("#3A1518"));

    /// زر المفضلة (لرسم القلب)
    public static IValueConverter Fav { get; } = new FuncValueConverter<string?, bool>(id => id == "fav");

    /// لون إطار القلب: أحمر في المفضلة
    public static IValueConverter HeartStroke { get; } = new FuncValueConverter<bool, IBrush>(on =>
        on ? new SolidColorBrush(Color.Parse("#EF4444")) : Brushes.White);

    public static IValueConverter HeartFill { get; } = new FuncValueConverter<bool, IBrush?>(on =>
        on ? new SolidColorBrush(Color.Parse("#EF4444")) : null);

    public static IValueConverter Positive { get; } = new FuncValueConverter<int, bool>(n => n > 0);

    /// شريط البطارية رقم N يضيء إن كان عدد الأشرطة ≥ N
    public static IValueConverter Bar { get; } = new BarConverter();

    private sealed class BarConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is int bars && int.TryParse(parameter as string, out var n) && bars >= n ? BarOn : BarOff;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

/// <summary>عناوين الإحصاءات بأحرف كبيرة في الإنجليزية (text-transform: uppercase).</summary>
public sealed class BpUpper
{
    public static BpUpper Instance { get; } = new();
    public string this[string key] => Loc.Instance.IsRtl ? Loc.Instance[key] : Loc.Instance[key].ToUpperInvariant();
}
