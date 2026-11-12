using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HalawaHub.Core.Store;

public sealed record SteamScreenshot(string ThumbnailUrl, string FullUrl);

public sealed record SteamAppDetails(
    string Name,
    string Description,
    string Developers,
    string Publishers,
    string ReleaseDate,
    string? HeaderImage,
    string WindowsRequirements,
    string LinuxRequirements,
    IReadOnlyList<SteamScreenshot> Screenshots);

/// توافق Steam Deck: 3 موثَّقة، 2 قابلة للعب، 1 غير مدعومة، 0 غير معروف
public enum DeckCompatibility
{
    Unknown = 0,
    Unsupported = 1,
    Playable = 2,
    Verified = 3
}

/// <summary>
/// بيانات صفحة اللعبة من متجر Steam، بلا مفتاح:
///   store.steampowered.com/api/appdetails?appids=&lt;id&gt; (عام، محدود المعدل)
///   store.steampowered.com/saleaction/ajaxgetdeckappcompatibilityreport?nAppID=&lt;id&gt; (غير رسمي)
/// الصيغتان مؤكدتان من ردود حقيقية (نوفمبر 2026). الردود تُحفظ على القرص أسبوعًا، وبلا
/// إنترنت تُستخدم آخر نسخة محفوظة مهما كان عمرها؛ أي فشل يعيد null بصمت.
/// </summary>
public static class SteamStoreClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    private static string CachePath(string name) => AppPaths.InData("cache", "store", name + ".json");

    public static async Task<SteamAppDetails?> GetDetailsAsync(string appId, string language)
    {
        var lang = language == "ar" ? "arabic" : "english";
        var json = await CachedAsync($"{appId}-{lang}", $"https://store.steampowered.com/api/appdetails?appids={appId}&l={lang}",
            j => ParseDetails(j, appId) != null);
        return ParseDetails(json, appId);
    }

    public static async Task<DeckCompatibility> GetDeckAsync(string appId)
    {
        var json = await CachedAsync($"{appId}-deck", $"https://store.steampowered.com/saleaction/ajaxgetdeckappcompatibilityreport?nAppID={appId}",
            j => j.Contains("resolved_category", StringComparison.Ordinal));
        return ParseDeck(json);
    }

    private static async Task<string?> CachedAsync(string name, string url, Func<string, bool> isGood)
    {
        var path = CachePath(name);
        string? cached = null;
        try
        {
            if (File.Exists(path))
            {
                cached = await File.ReadAllTextAsync(path);
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < MaxAge) return cached;
            }
        }
        catch { /* نجلب من الشبكة */ }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("HalawaHub/1.0");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return cached;
            var fresh = await response.Content.ReadAsStringAsync();
            if (!isGood(fresh)) return cached;
            try { AtomicFile.WriteAllText(path, fresh); } catch (Exception ex) { Log.Error("store cache save failed", ex); }
            return fresh;
        }
        catch (Exception ex)
        {
            Log.Info($"Steam store: fetch failed for {name} ({ex.Message})");
            return cached;
        }
    }

    internal static SteamAppDetails? ParseDetails(string? json, string appId)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(appId, out var app)) return null;
            if (!app.TryGetProperty("success", out var ok) || ok.ValueKind != JsonValueKind.True) return null;
            if (!app.TryGetProperty("data", out var d) || d.ValueKind != JsonValueKind.Object) return null;

            var shots = new List<SteamScreenshot>();
            if (d.TryGetProperty("screenshots", out var ss) && ss.ValueKind == JsonValueKind.Array)
                foreach (var s in ss.EnumerateArray())
                {
                    var thumb = Str(s, "path_thumbnail");
                    var full = Str(s, "path_full");
                    if (IsHttps(thumb) && IsHttps(full)) shots.Add(new SteamScreenshot(thumb!, full!));
                }

            var header = Str(d, "header_image");
            return new SteamAppDetails(
                Str(d, "name") ?? "",
                HtmlToText(Str(d, "short_description")),
                Join(d, "developers"),
                Join(d, "publishers"),
                d.TryGetProperty("release_date", out var rd) ? Str(rd, "date") ?? "" : "",
                IsHttps(header) ? header : null,
                Requirements(d, "pc_requirements"),
                Requirements(d, "linux_requirements"),
                shots);
        }
        catch (Exception ex)
        {
            Log.Info("Steam store: unexpected appdetails format (" + ex.Message + ")");
            return null;
        }
    }

    internal static DeckCompatibility ParseDeck(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return DeckCompatibility.Unknown;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Object
                && r.TryGetProperty("resolved_category", out var c) && c.ValueKind == JsonValueKind.Number
                && c.GetInt32() is >= 0 and <= 3 and var value)
                return (DeckCompatibility)value;
        }
        catch { /* غير معروف */ }
        return DeckCompatibility.Unknown;
    }

    /// المتطلبات تأتي كائنًا {minimum, recommended} بـ HTML، أو مصفوفة فارغة إن لم توجد
    private static string Requirements(JsonElement d, string name)
    {
        if (!d.TryGetProperty(name, out var req) || req.ValueKind != JsonValueKind.Object) return "";
        var parts = new[] { HtmlToText(Str(req, "minimum")), HtmlToText(Str(req, "recommended")) };
        return string.Join("\n\n", parts.Where(p => p.Length > 0));
    }

    /// HTML بسيط من Steam → نص: فواصل الأسطر والقوائم تصير أسطرًا، وتُحذف بقية الوسوم
    internal static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = Regex.Replace(html, @"<\s*(br|/li|/p|/ul|/h\d)\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\s*\n\s*", "\n");
        return text.Trim();
    }

    private static bool IsHttps(string? url) => url != null && url.StartsWith("https://", StringComparison.Ordinal);

    private static string? Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Join(JsonElement d, string name) =>
        d.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? string.Join(", ", arr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()))
            : "";
}
