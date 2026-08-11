using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace HalawaHub.App.Behaviors;

/// <summary>
/// خاصيتان مرفقتان (Attached Properties) على Image: SourceUrl (الرابط الأساسي)
/// وFallbackUrl (يُجرَّب تلقائيًا لو فشل الأساسي). تحمّل الصورة بالخلفية
/// بدون ما تجمّد الواجهة، مع كاش بالذاكرة.
///
/// الاستخدام بالـ XAML:
///   xmlns:behaviors="using:HalawaHub.App.Behaviors"
///   &lt;Image behaviors:AsyncImage.SourceUrl="{Binding CoverImageUrl}"
///          behaviors:AsyncImage.FallbackUrl="{Binding CoverImageUrlFallback}" /&gt;
/// </summary>
public static class AsyncImage
{
    public static readonly AttachedProperty<string?> SourceUrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("SourceUrl", typeof(AsyncImage));

    public static readonly AttachedProperty<string?> FallbackUrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("FallbackUrl", typeof(AsyncImage));

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // بعض شبكات CDN (زي Cloudflare) ترفض الطلبات اللي ما فيها User-Agent
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) HalawaHub/1.0");
        return client;
    }

    // كاش بالذاكرة عشان ما نعيد تحميل/فك ترميز نفس الصورة كل ما تتحرك القائمة
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    static AsyncImage()
    {
        SourceUrlProperty.Changed.AddClassHandler<Image>(OnSourceUrlChanged);
    }

    public static string? GetSourceUrl(Image element) => element.GetValue(SourceUrlProperty);
    public static void SetSourceUrl(Image element, string? value) => element.SetValue(SourceUrlProperty, value);

    public static string? GetFallbackUrl(Image element) => element.GetValue(FallbackUrlProperty);
    public static void SetFallbackUrl(Image element, string? value) => element.SetValue(FallbackUrlProperty, value);

    private static async void OnSourceUrlChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        var url = e.NewValue as string;
        image.Source = null;

        if (string.IsNullOrEmpty(url))
        {
            // ما فيه رابط أساسي، بس ممكن فيه احتياطي (نادر لكن نتعامل معه)
            var fb = GetFallbackUrl(image);
            if (!string.IsNullOrEmpty(fb)) await LoadInto(image, fb, null);
            return;
        }

        await LoadInto(image, url, GetFallbackUrl(image));
    }

    private static async System.Threading.Tasks.Task LoadInto(Image image, string url, string? fallbackUrl)
    {
        if (Cache.TryGetValue(url, out var cached))
        {
            if (cached != null)
            {
                if (GetSourceUrl(image) == url) image.Source = cached;
                return;
            }
            // فشل سابق مسجّل بالكاش لهذا الرابط — جرّب الاحتياطي مباشرة
            if (!string.IsNullOrEmpty(fallbackUrl)) await LoadInto(image, fallbackUrl, null);
            return;
        }

        try
        {
            // نحمّل البايتات كاملة أول (Stream غير قابل للـ Seek مباشر من الشبكة
            // يسبب مشاكل فك ترميز مع بعض المكتبات)، ثم نفك الترميز من ذاكرة قابلة للـ Seek
            var bytes = await Http.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);

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
            // فشل التحميل — نسجّله كـ null بالكاش عشان ما نعيد محاولة فاشلة
            // لنفس الرابط كل مرة، ونجرّب الاحتياطي لو موجود
            Cache[url] = null;

            if (!string.IsNullOrEmpty(fallbackUrl))
                await LoadInto(image, fallbackUrl, null);
        }
    }
}
