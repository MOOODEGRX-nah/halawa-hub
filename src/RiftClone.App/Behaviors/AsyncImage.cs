using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace RiftClone.App.Behaviors;

/// <summary>
/// خاصية مرفقة (Attached Property) على Image تحمّل الصورة من رابط بالخلفية
/// وتعرضها بمجرد ما تجهز، بدون ما تجمّد الواجهة أثناء التحميل.
///
/// الاستخدام بالـ XAML:
///   xmlns:behaviors="using:RiftClone.App.Behaviors"
///   &lt;Image behaviors:AsyncImage.SourceUrl="{Binding CoverImageUrl}" /&gt;
/// </summary>
public static class AsyncImage
{
    public static readonly AttachedProperty<string?> SourceUrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("SourceUrl", typeof(AsyncImage));

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // كاش بالذاكرة عشان ما نعيد تحميل/فك ترميز نفس الصورة كل ما تتحرك القائمة
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    static AsyncImage()
    {
        SourceUrlProperty.Changed.AddClassHandler<Image>(OnSourceUrlChanged);
    }

    public static string? GetSourceUrl(Image element) => element.GetValue(SourceUrlProperty);
    public static void SetSourceUrl(Image element, string? value) => element.SetValue(SourceUrlProperty, value);

    private static async void OnSourceUrlChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        var url = e.NewValue as string;
        image.Source = null;

        if (string.IsNullOrEmpty(url)) return;

        if (Cache.TryGetValue(url, out var cached))
        {
            image.Source = cached;
            return;
        }

        try
        {
            await using var stream = await Http.GetStreamAsync(url);
            // فك ترميز مصغّر (300 بكسل عرض) بدل الحجم الأصلي، يوفر ذاكرة كبيرة
            // لما تكون عندك مكتبة فيها مئات الألعاب بنفس الوقت
            var bitmap = Bitmap.DecodeToWidth(stream, 300);

            Cache[url] = bitmap;

            // تأكد إن نفس عنصر الصورة لسه يعرض نفس الرابط (القوائم تعيد تدوير عناصرها)
            if (GetSourceUrl(image) == url)
                image.Source = bitmap;
        }
        catch
        {
            // فشل التحميل (رابط غير موجود، لا إنترنت...) — نسجّله كـ null بالكاش
            // عشان ما نعيد محاولة فاشلة لنفس الرابط كل مرة يتحرك فيها العنصر
            Cache[url] = null;
        }
    }
}
