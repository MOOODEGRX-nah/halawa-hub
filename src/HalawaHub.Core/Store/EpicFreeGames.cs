using System.Net.Http;
using System.Text.Json;

namespace HalawaHub.Core.Store;

public sealed record FreeGame(string Title, string Url, string? ImageUrl, DateTime EndsUtc);

/// <summary>
/// "مجانية هذا الأسبوع" من Epic: مصدر عام يستخدمه موقع المتجر نفسه لكنه غير موثّق رسميًا،
/// فأي فشل (شبكة، تغيّر الصيغة) يعيد قائمة فارغة ويُخفى القسم. الصيغة مؤكدة من رد حقيقي
/// (نوفمبر 2026): data.Catalog.searchStore.elements[] وفيها promotions.promotionalOffers[]
/// .promotionalOffers[] {startDate, endDate, discountSetting.discountPercentage = 0}.
/// </summary>
public static class EpicFreeGames
{
    public const string Endpoint =
        "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<string?> FetchJsonAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.UserAgent.ParseAdd("HalawaHub/1.0");
            using var response = await Http.SendAsync(request);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
        }
        catch (Exception ex)
        {
            Log.Info("Epic free games: fetch failed (" + ex.Message + ")");
            return null;
        }
    }

    /// الألعاب المجانية الآن فقط (خصم 100% ساري المفعول)
    public static List<FreeGame> Parse(string? json, DateTime utcNow)
    {
        var result = new List<FreeGame>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryPath(doc.RootElement, out var elements, "data", "Catalog", "searchStore", "elements")
                || elements.ValueKind != JsonValueKind.Array) return result;

            foreach (var el in elements.EnumerateArray())
            {
                var title = Str(el, "title");
                if (string.IsNullOrEmpty(title)) continue;
                if (ActiveFreeOfferEnd(el, utcNow) is not { } ends) continue;

                var slug = PageSlug(el);
                if (string.IsNullOrEmpty(slug)) continue;
                result.Add(new FreeGame(title, "https://store.epicgames.com/p/" + Uri.EscapeDataString(slug).Replace("%2F", "/"), Image(el), ends));
            }
        }
        catch (Exception ex)
        {
            Log.Info("Epic free games: unexpected format (" + ex.Message + ")");
            result.Clear();
        }
        return result;
    }

    private static DateTime? ActiveFreeOfferEnd(JsonElement el, DateTime utcNow)
    {
        if (!TryPath(el, out var groups, "promotions", "promotionalOffers") || groups.ValueKind != JsonValueKind.Array) return null;
        foreach (var group in groups.EnumerateArray())
        {
            if (!group.TryGetProperty("promotionalOffers", out var offers) || offers.ValueKind != JsonValueKind.Array) continue;
            foreach (var offer in offers.EnumerateArray())
            {
                if (!TryPath(offer, out var pct, "discountSetting", "discountPercentage") || pct.ValueKind != JsonValueKind.Number || pct.GetInt32() != 0) continue;
                if (!DateTime.TryParse(Str(offer, "startDate"), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var start)) continue;
                if (!DateTime.TryParse(Str(offer, "endDate"), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var end)) continue;
                if (start <= utcNow && utcNow < end) return end;
            }
        }
        return null;
    }

    private static string? PageSlug(JsonElement el)
    {
        if (TryPath(el, out var mappings, "catalogNs", "mappings") && mappings.ValueKind == JsonValueKind.Array)
            foreach (var m in mappings.EnumerateArray())
                if (Str(m, "pageSlug") is { Length: > 0 } slug) return slug;
        var product = Str(el, "productSlug");
        return string.IsNullOrEmpty(product) ? null : product.Replace("/home", "");
    }

    private static string? Image(JsonElement el)
    {
        if (!el.TryGetProperty("keyImages", out var images) || images.ValueKind != JsonValueKind.Array) return null;
        string? any = null;
        foreach (var img in images.EnumerateArray())
        {
            var url = Str(img, "url");
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.Ordinal)) continue;
            var type = Str(img, "type");
            if (type == "OfferImageWide") return url;
            if (type is "Thumbnail" or "OfferImageTall") any ??= url;
        }
        return any;
    }

    private static string? Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool TryPath(JsonElement el, out JsonElement value, params string[] path)
    {
        value = el;
        foreach (var p in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(p, out value)) return false;
        }
        return true;
    }
}
