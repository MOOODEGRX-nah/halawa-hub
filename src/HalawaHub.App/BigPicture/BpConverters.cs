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
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#EF4444"));
    private static readonly IBrush Text = new SolidColorBrush(Color.Parse("#F5EDED"));
    private static readonly IBrush Faded = new SolidColorBrush(Color.Parse("#9C8A8A"));
    private static readonly IBrush Pink = new SolidColorBrush(Color.Parse("#FCA5A5"));

    // الفرش ثابتة: فرشاة جديدة مع كل تحديث تُعيد رسم العنصر بلا داعٍ
    public static IValueConverter HeartStroke { get; } = new FuncValueConverter<bool, IBrush>(on => on ? Red : Brushes.White);

    public static IValueConverter HeartFill { get; } = new FuncValueConverter<bool, IBrush?>(on => on ? Red : null);

    public static IValueConverter QueryBrush { get; } = new FuncValueConverter<bool, IBrush>(has => has ? Text : Faded);

    /// عرض مفتاح لوحة المفاتيح: المسافة 490، الحذف والتبديل وتم 190، والحروف 90
    public static IValueConverter KeyWidth { get; } = new FuncValueConverter<string?, double>(id => id switch
    {
        "space" => 490, "del" or "abc" or "done" => 190, _ => 90,
    });

    public static IValueConverter KeyFont { get; } = new FuncValueConverter<string?, double>(id =>
        id is "space" or "abc" or "done" ? 21 : 28);

    /// المؤشر الجانبي يخفت حين يكون التركيز على الأسطر
    public static IValueConverter Dim { get; } = new FuncValueConverter<bool, double>(dim => dim ? 0.4 : 1);

    public static IValueConverter FailTitle { get; } = new FuncValueConverter<bool, IBrush>(bad => bad ? Pink : Text);

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

/// <summary>شكل الإشعار حسب نوعه (.nic.upd / .free / .scan / .fail / .app).</summary>
public static class BpKind
{
    private static readonly System.Collections.Generic.Dictionary<string, (string Path, string Bg, string Border, string Fg)> Kinds = new()
    {
        ["upd"] = ("M12 4v12M6 11l6 6 6-6M5 20h14", "#2A1313", "#4A2A12", "#F5A623"),
        ["free"] = ("M20 12v9H4v-9M2 7h20v5H2zM12 21V7M12 7H8a2.5 2.5 0 0 1 0-5c3 0 4 5 4 5zM12 7h4a2.5 2.5 0 0 0 0-5c-3 0-4 5-4 5z", "#33157F3D", "#664ADE80", "#4ADE80"),
        ["scan"] = ("M5 12.5l4.5 4.5L19 7.5", "#1C0E0F", "#2E1616", "#D9C7C7"),
        ["fail"] = ("M12 8v5M12 16.5v.5M10.3 3.9L2.4 18a2 2 0 0 0 1.7 3h15.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z", "#597F1D1D", "#8CEF4444", "#FCA5A5"),
        ["app"] = ("M21 12a9 9 0 1 1-3-6.7L21 8M21 3v5h-5", "#DC2626", "#00000000", "#FFFFFF"),
    };

    private static (string Path, string Bg, string Border, string Fg) Of(string? kind) =>
        kind != null && Kinds.TryGetValue(kind, out var k) ? k : Kinds["scan"];

    private static readonly System.Collections.Generic.Dictionary<string, object> Built = new();

    private static T Cached<T>(string? kind, string part, Func<string, T> make) where T : class
    {
        var key = (kind ?? "scan") + "|" + part;
        if (Built.TryGetValue(key, out var v)) return (T)v;
        var made = make(part switch { "p" => Of(kind).Path, "b" => Of(kind).Bg, "o" => Of(kind).Border, _ => Of(kind).Fg });
        Built[key] = made;
        return made;
    }

    public static IValueConverter PathOf { get; } = new FuncValueConverter<string?, Geometry>(k => Cached(k, "p", Geometry.Parse));
    public static IValueConverter Background { get; } = new FuncValueConverter<string?, IBrush>(k => Cached<IBrush>(k, "b", s => new SolidColorBrush(Color.Parse(s))));
    public static IValueConverter Border { get; } = new FuncValueConverter<string?, IBrush>(k => Cached<IBrush>(k, "o", s => new SolidColorBrush(Color.Parse(s))));
    public static IValueConverter Foreground { get; } = new FuncValueConverter<string?, IBrush>(k => Cached<IBrush>(k, "f", s => new SolidColorBrush(Color.Parse(s))));
}
