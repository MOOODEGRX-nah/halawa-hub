using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace HalawaHub.Core.Store;

public sealed record StoreDeal(string Store, string Title, string Url, string? ImageUrl, int DiscountPercent, string PriceText, string OldPriceText);

/// <summary>
/// تخفيضات المتاجر لصفحة العروض. مصدران عامان بلا مفتاح، يستخدمهما موقعا المتجرين، والصيغة
/// مؤكدة من ردود حقيقية (نوفمبر 2026):
/// Steam: store.steampowered.com/api/featuredcategories ← specials.items[] {id, type, name,
/// discounted, discount_percent, original_price, final_price (بأصغر وحدة للعملة), currency, header_image}.
/// السعر والعملة حسب بلد الاتصال. GOG: catalog.gog.com/v1/catalog?discounted=eq:true ← products[]
/// {title, storeLink, coverHorizontal, price {final, base, discount "-50%"}}.
/// أي فشل (شبكة، تغيّر الصيغة) يعيد قائمة فارغة ويُخفى القسم.
/// </summary>
public static class StoreDeals
{
    public const string SteamEndpoint = "https://store.steampowered.com/api/featuredcategories?l=english";
    public const string GogEndpoint = "https://catalog.gog.com/v1/catalog?limit=12&order=desc:trending&discounted=eq:true&productType=in:game";

    public const int MaxPerStore = 12;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<string?> FetchJsonAsync(string endpoint)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.UserAgent.ParseAdd("HalawaHub/1.0");
            using var response = await Http.SendAsync(request);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
        }
        catch (Exception ex)
        {
            Log.Info("store deals: fetch failed (" + ex.Message + ")");
            return null;
        }
    }

    public static List<StoreDeal> ParseSteam(string? json)
    {
        var result = new List<StoreDeal>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("specials", out var specials) ||
                specials.ValueKind != JsonValueKind.Object || !specials.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return result;

            var seen = new HashSet<long>();
            foreach (var el in items.EnumerateArray())
            {
                if (result.Count >= MaxPerStore) break;
                if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number ||
                    !idEl.TryGetInt64(out var id) || !seen.Add(id)) continue;
                var title = Text(el, "name");
                var percent = Int(el, "discount_percent");
                if (title.Length == 0 || percent <= 0 || percent > 100) continue;

                // type: 0 لعبة، 1 حزمة (sub)، 2 مجموعة (bundle)
                var kind = Int(el, "type") switch { 1 => "sub", 2 => "bundle", _ => "app" };
                var currency = Text(el, "currency");
                var image = Text(el, "header_image");
                if (image.Length == 0) image = Text(el, "large_capsule_image");
                result.Add(new StoreDeal("Steam", title, $"https://store.steampowered.com/{kind}/{id}",
                    IsHttps(image) ? image : null, percent,
                    Money(Int(el, "final_price"), currency), Money(Int(el, "original_price"), currency)));
            }
        }
        catch (Exception ex)
        {
            Log.Info("Steam deals: unreadable answer (" + ex.Message + ")");
            result.Clear();
        }
        return result;
    }

    public static List<StoreDeal> ParseGog(string? json)
    {
        var result = new List<StoreDeal>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array) return result;

            foreach (var el in products.EnumerateArray())
            {
                if (result.Count >= MaxPerStore) break;
                if (el.ValueKind != JsonValueKind.Object) continue;
                var title = Text(el, "title");
                var url = Text(el, "storeLink");
                if (title.Length == 0 || !url.StartsWith("https://www.gog.com/", StringComparison.OrdinalIgnoreCase)) continue;
                if (!el.TryGetProperty("price", out var price) || price.ValueKind != JsonValueKind.Object) continue;

                var digits = new string(Text(price, "discount").Where(char.IsDigit).ToArray());
                if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var percent) || percent <= 0 || percent > 100) continue;

                var image = Text(el, "coverHorizontal");
                result.Add(new StoreDeal("GOG", title, url, IsHttps(image) ? image : null, percent, Text(price, "final"), Text(price, "base")));
            }
        }
        catch (Exception ex)
        {
            Log.Info("GOG deals: unreadable answer (" + ex.Message + ")");
            result.Clear();
        }
        return result;
    }

    /// 3180 + SAR ← "31.80 SAR"
    internal static string Money(int minorUnits, string currency)
    {
        if (minorUnits < 0) return "";
        var text = (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        return currency.Length == 0 ? text : text + " " + currency;
    }

    private static bool IsHttps(string url) => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string Text(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int Int(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}
