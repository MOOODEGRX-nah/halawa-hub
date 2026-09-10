using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace HalawaHub.App.Behaviors;

/// <summary>
/// خاصيتان مرفقتان (Attached Properties) على Image: SourceUrl (الرابط الأساسي)
/// وFallbackUrl (يُجرَّب تلقائيًا لو فشل الأساسي). تحمّل الصورة بالخلفية
/// بدون ما تجمّد الواجهة، مع كاش بالذاكرة.
///
/// v0.0.10: التحميل صار كسول (Lazy) — ما نبدأ تنزيل/فك ترميز أي غلاف إلا
/// لما عنصر Image يصير فعليًا مرئي جوّا ScrollViewer (عبر EffectiveViewportChanged)،
/// مع حد أقصى 6 تحميلات متزامنة عشان ما تنطلق كل الأغلفة دفعة وحدة حتى لو كانت
/// كلها مرئية بنفس اللحظة. لو المستخدم مرّر بسرعة وطلعت البطاقة عن الشاشة قبل
/// ما يخلص التحميل، نلغيه فورًا بدل ما نكمله بالفاضي.
///
/// ملاحظة: هذا يحل تكلفة الشبكة/فك الترميز (أكبر مصدر استهلاك رام)، لكنه لا يلغي
/// إنشاء عناصر الواجهة (Border/Grid) لكل الألعاب دفعة وحدة — WrapPanel داخل
/// ListBox ما يدعم تصغير العناصر خارج الشاشة (UI Virtualization) أصلاً. لو
/// بعد هذا التعديل الرام لسه فوق الهدف، الخطوة اللي بعدها تكون استبدال الـ
/// WrapPanel بحل يدعم virtualization حقيقي — تغيير أكبر ويحتاج اختبار فعلي.
///
/// الاستخدام بالـ XAML ما تغيّر:
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

    // يحدّ عدد التحميلات (شبكة + فك ترميز) الشغّالة بنفس اللحظة مهما كان عدد
    // الأغلفة المرئية دفعة وحدة (مثلاً أول ما تفتح صفحة فيها عشرات البطاقات)
    private static readonly SemaphoreSlim ConcurrencyLimiter = new(6);

    // حالة كل عنصر Image على حدة — Weak عشان ما يسبب تسريب ذاكرة لو انحذف العنصر
    private static readonly ConditionalWeakTable<Image, ImageLoadState> States = new();

    private sealed class ImageLoadState
    {
        public string? PendingUrl;
        public string? FallbackUrl;
        public string? LoadedUrl;
        public bool IsVisible;
        public bool IsLoading;
        public bool TrackingAttached;
        public CancellationTokenSource? Cts;
    }

    static AsyncImage()
    {
        SourceUrlProperty.Changed.AddClassHandler<Image>(OnSourceUrlChanged);
    }

    public static string? GetSourceUrl(Image element) => element.GetValue(SourceUrlProperty);
    public static void SetSourceUrl(Image element, string? value) => element.SetValue(SourceUrlProperty, value);

    public static string? GetFallbackUrl(Image element) => element.GetValue(FallbackUrlProperty);
    public static void SetFallbackUrl(Image element, string? value) => element.SetValue(FallbackUrlProperty, value);

    private static ImageLoadState GetState(Image image) => States.GetValue(image, _ => new ImageLoadState());

    private static void OnSourceUrlChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        var state = GetState(image);

        // رابط جديد جانا وفيه تحميل سابق شغّال أو منتظر بالطابور — ألغيه
        CancelPending(state);

        image.Source = null;
        state.PendingUrl = e.NewValue as string;
        state.FallbackUrl = GetFallbackUrl(image);
        state.LoadedUrl = null;

        EnsureViewportTracking(image, state);

        // لو العنصر مرئي حاليًا، ابدأ التحميل فورًا. غير كذا ننتظر
        // EffectiveViewportChanged لما يدخل الشاشة فعليًا
        if (state.IsVisible)
            _ = TryStartLoad(image, state);
    }

    private static void EnsureViewportTracking(Image image, ImageLoadState state)
    {
        if (state.TrackingAttached) return;
        state.TrackingAttached = true;

        // نفترض غير مرئي لحد ما نستلم أول EffectiveViewportChanged — يوصل بسرعة
        // بعد أول Layout سواء كان العنصر داخل أو خارج نطاق الرؤية الحالي
        state.IsVisible = false;

        void OnEffectiveViewportChanged(object? _, EffectiveViewportChangedEventArgs args)
        {
            var vp = args.EffectiveViewport;
            var wasVisible = state.IsVisible;

            // لو ما فيه ScrollViewer يقيّد المساحة (عرض/ارتفاع لانهائي)، نعتبره
            // مرئي دايمًا بدل ما نعطّل تحميله بالغلط
            state.IsVisible = double.IsInfinity(vp.Width) || double.IsInfinity(vp.Height)
                || (vp.Width > 0 && vp.Height > 0);

            if (state.IsVisible && !wasVisible)
                _ = TryStartLoad(image, state);
            else if (!state.IsVisible && wasVisible)
                CancelPending(state); // طلع عن الشاشة قبل ما يخلص التحميل — وفّر شبكة وذاكرة
        }

        // مهم: الاشتراك بـ EffectiveViewportChanged ما يسجّل شي فعليًا عند
        // Avalonia's LayoutManager إلا لو العنصر متصل بشجرة مرئية (VisualRoot
        // موجود) وقت الاشتراك — فلازم نشترك جوّا AttachedToVisualTree، مو مباشرة
        image.AttachedToVisualTree += (_, _) =>
            image.EffectiveViewportChanged += OnEffectiveViewportChanged;

        image.DetachedFromVisualTree += (_, _) =>
        {
            image.EffectiveViewportChanged -= OnEffectiveViewportChanged;
            state.IsVisible = false;
            CancelPending(state);
        };

        // العنصر ممكن يكون متصل بالشجرة أصلاً وقت ما وصلنا هنا (مثلاً لو الرابط
        // تغيّر بعد إنشاء البطاقة بفترة) — AttachedToVisualTree ما راح ينطلق
        // مرة ثانية بهالحالة، فنشترك فورًا
        if (image.GetVisualRoot() != null)
            image.EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    private static void CancelPending(ImageLoadState state)
    {
        state.Cts?.Cancel();
        state.Cts?.Dispose();
        state.Cts = null;
        state.IsLoading = false;
    }

    private static Task TryStartLoad(Image image, ImageLoadState state)
    {
        if (state.IsLoading) return Task.CompletedTask;

        var url = state.PendingUrl;
        if (string.IsNullOrEmpty(url))
        {
            // ما فيه رابط أساسي، بس ممكن فيه احتياطي (نادر لكن نتعامل معه)
            return string.IsNullOrEmpty(state.FallbackUrl)
                ? Task.CompletedTask
                : StartLoad(image, state, state.FallbackUrl!, null);
        }

        return StartLoad(image, state, url, state.FallbackUrl);
    }

    private static async Task StartLoad(Image image, ImageLoadState state, string url, string? fallbackUrl)
    {
        if (state.LoadedUrl == url) return;

        if (Cache.TryGetValue(url, out var cached))
        {
            if (cached != null)
            {
                if (state.PendingUrl == url)
                {
                    image.Source = cached;
                    state.LoadedUrl = url;
                }
                return;
            }
            // فشل سابق مسجّل بالكاش لهذا الرابط — جرّب الاحتياطي مباشرة
            if (!string.IsNullOrEmpty(fallbackUrl)) await StartLoad(image, state, fallbackUrl!, null);
            return;
        }

        state.IsLoading = true;
        var cts = new CancellationTokenSource();
        state.Cts = cts;
        var token = cts.Token;
        var acquired = false;
        var failed = false;

        try
        {
            // ننتظر دورنا بالطابور — أقصى 6 تحميلات بنفس الوقت
            // مهم: بدون ConfigureAwait(false) — لازم نرجع لخيط الواجهة لأن هذي
            // الدالة تعدّل image.Source لاحقًا، وأي تعديل واجهة من خيط ثاني خطأ
            await ConcurrencyLimiter.WaitAsync(token);
            acquired = true;

            // لو العنصر طلع عن الشاشة أو تغيّر رابطه وهو منتظر بالطابور، لا داعي نكمل
            if (token.IsCancellationRequested || state.PendingUrl != url) return;

            byte[] bytes;
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                // غلاف محلي اختاره المستخدم يدويًا
                var localPath = new Uri(url).LocalPath;
                bytes = await File.ReadAllBytesAsync(localPath, token);
            }
            else
            {
                // نحمّل البايتات كاملة أول (Stream غير قابل للـ Seek مباشر من الشبكة
                // يسبب مشاكل فك ترميز مع بعض المكتبات)، ثم نفك الترميز من ذاكرة قابلة للـ Seek
                bytes = await Http.GetByteArrayAsync(url, token);
            }

            token.ThrowIfCancellationRequested();

            using var stream = new MemoryStream(bytes);

            // فك ترميز مصغّر (300 بكسل عرض) بدل الحجم الأصلي، يوفر ذاكرة كبيرة
            // لما تكون عندك مكتبة فيها مئات الألعاب بنفس الوقت
            var bitmap = Bitmap.DecodeToWidth(stream, 300);

            Cache[url] = bitmap;

            // تأكد إن نفس عنصر الصورة لسه يعرض نفس الرابط (القوائم تعيد تدوير عناصرها)
            if (!token.IsCancellationRequested && state.PendingUrl == url)
            {
                image.Source = bitmap;
                state.LoadedUrl = url;
            }
        }
        catch (OperationCanceledException)
        {
            // خرج عن الشاشة أو تغيّر رابطه قبل ما يخلص — تجاهل بهدوء، بدون تسجيله
            // كفشل بالكاش (ممكن ننجح لو حاولنا مرة ثانية لما يرجع يصير مرئي)
        }
        catch
        {
            // فشل حقيقي (شبكة/فك ترميز) — نسجّله كـ null بالكاش عشان ما نعيد
            // محاولة فاشلة لنفس الرابط كل مرة
            Cache[url] = null;
            failed = true;
        }
        finally
        {
            if (acquired) ConcurrencyLimiter.Release();
            state.IsLoading = false;
            if (state.Cts == cts) state.Cts = null;
        }

        // نجرب الاحتياطي بعد إطلاق مكاننا بالطابور (مو جوّا finally) عشان ما نحجز
        // مكانين بالطابور بنفس الوقت لنفس البطاقة
        if (failed && !token.IsCancellationRequested && !string.IsNullOrEmpty(fallbackUrl))
            await StartLoad(image, state, fallbackUrl!, null);
    }
}
