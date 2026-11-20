using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HalawaHub.App.BigPicture;

/// <summary>ألوان بطاقة بلا غلاف: تدرج من ألوان التصميم، ثابت لكل لعبة.</summary>
public static class BpArt
{
    private static readonly string[] Tops =
    {
        "#6b1f3a", "#9f1239", "#7c2d12", "#7f1d1d", "#78350f", "#831843", "#713f12", "#9d174d", "#854d0e", "#4c1d2e", "#881337", "#6b2121",
    };
    private static readonly string[] Bottoms =
    {
        "#12060b", "#1a0508", "#150704", "#0f0404", "#140a03", "#14060d", "#120903", "#16050c", "#140c02", "#0b0407", "#13050a", "#100404",
    };
    private static readonly Dictionary<string, IBrush> Cache = new();

    public static IBrush For(string? name)
    {
        name ??= "";
        if (Cache.TryGetValue(name, out var cached)) return cached;
        var i = (int)((uint)StableHash(name) % (uint)Tops.Length);
        // linear-gradient(160deg, top, bottom)
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.33, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.67, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse(Tops[i]), 0), new GradientStop(Color.Parse(Bottoms[i]), 1) },
        };
        brush.ToImmutable();
        Cache[name] = brush;
        return brush;
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 17;
            foreach (var ch in s) h = h * 31 + ch;
            return h;
        }
    }
}

/// <summary>اسم اللعبة → تدرج البطاقة.</summary>
public sealed class BpArtConverter : IValueConverter
{
    public static BpArtConverter Instance { get; } = new();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => BpArt.For(value as string);
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
