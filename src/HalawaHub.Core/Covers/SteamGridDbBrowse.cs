using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace HalawaHub.Core.Covers;

/// <summary>أنواع الصور في SteamGridDB كما تظهر في المتصفح: أغلفة، أغلفة عريضة، خلفيات، شعارات، أيقونات.</summary>
public enum SgdbArtType { Cover, Wide, Background, Logo, Icon }

public sealed record SgdbGame(int Id, string Name);

/// صورة واحدة من نتائج SteamGridDB. Score هو تقييم المجتمع (يظهر كقلب في التصميم).
public sealed record SgdbImage(int Id, string Url, string Thumb, string Style, int Score, bool IsAnimated, int Width, int Height);

/// صفحة نتائج. Total هو العدد الكلي عند SteamGridDB لهذا الاستعلام.
public sealed record SgdbPage(IReadOnlyList<SgdbImage> Items, int Total);

/// <summary>
/// تصفح SteamGridDB API v2 (المرجع: https://www.steamgriddb.com/static/openapi.yml):
/// GET /grids|heroes|logos|icons/game/{id} مع styles و dimensions و types (static, animated) و limit (50 كحد أقصى) و page (يبدأ من 0).
/// الرد: { success, page, total, limit, data: [ { id, score, style, url, thumb, width, height, ... } ] }.
/// النوع الافتراضي عند SteamGridDB هو static فقط، فالمتحرك يُطلب صراحة.
/// </summary>
public static class SgdbQuery
{
    public const int PageSize = 50;

    /// الأشكال التي يقبلها كل نوع (Styles / StylesHero / StylesLogo / StylesIcon في openapi.yml)
    public static IReadOnlyList<string> StylesFor(SgdbArtType type) => type switch
    {
        SgdbArtType.Cover or SgdbArtType.Wide => new[] { "alternate", "blurred", "white_logo", "no_logo", "material" },
        SgdbArtType.Background => new[] { "alternate", "blurred", "material" },
        SgdbArtType.Logo => new[] { "official", "white", "black", "custom" },
        _ => new[] { "official", "custom" }
    };

    /// الأغلفة العمودية مقابل العريضة: كلاهما من /grids ويفرق بينهما المقاس (Dimensions في openapi.yml)
    public static string? DimensionsFor(SgdbArtType type) => type switch
    {
        SgdbArtType.Cover => "600x900,342x482,660x930",
        SgdbArtType.Wide => "460x215,920x430",
        _ => null
    };

    private static string Endpoint(SgdbArtType type) => type switch
    {
        SgdbArtType.Background => "heroes",
        SgdbArtType.Logo => "logos",
        SgdbArtType.Icon => "icons",
        _ => "grids"
    };

    /// مسار الطلب نسبةً إلى https://www.steamgriddb.com/api/v2/
    public static string BuildPath(int gameId, SgdbArtType type, string? style, bool animated, int page)
    {
        var parts = new List<string> { "types=" + (animated ? "animated" : "static") };
        if (!string.IsNullOrEmpty(style) && Array.IndexOf(ToArray(StylesFor(type)), style) >= 0) parts.Add("styles=" + style);
        if (DimensionsFor(type) is { } dims) parts.Add("dimensions=" + dims);
        parts.Add("limit=" + PageSize);
        parts.Add("page=" + Math.Max(0, page));
        return $"{Endpoint(type)}/game/{gameId}?{string.Join("&", parts)}";
    }

    private static string[] ToArray(IReadOnlyList<string> list)
    {
        var a = new string[list.Count];
        for (var i = 0; i < a.Length; i++) a[i] = list[i];
        return a;
    }

    public static SgdbPage ParsePage(string json, bool animated)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var items = new List<SgdbImage>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in data.EnumerateArray())
            {
                var url = Str(e, "url");
                if (string.IsNullOrEmpty(url)) continue;
                var thumb = Str(e, "thumb");
                items.Add(new SgdbImage(
                    Int(e, "id"), url, string.IsNullOrEmpty(thumb) ? url : thumb,
                    Str(e, "style") ?? "", Int(e, "score"), animated, Int(e, "width"), Int(e, "height")));
            }
        }
        var total = root.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : items.Count;
        return new SgdbPage(items, total);
    }

    public static IReadOnlyList<SgdbGame> ParseGames(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<SgdbGame>();
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var e in data.EnumerateArray())
                if (Str(e, "name") is { Length: > 0 } name) list.Add(new SgdbGame(Int(e, "id"), name));
        return list;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;
}

public partial class SteamGridDbClient
{
    private const string ApiBase = "https://www.steamgriddb.com/api/v2/";

    /// أقرب لعبة للاسم (أول نتيجة في autocomplete)، أو null إن لم توجد. يرمي HttpRequestException عند انقطاع الإنترنت.
    public virtual async Task<SgdbGame?> FindGameAsync(string name, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(name)) return null;
        var json = await _http.GetStringAsync(ApiBase + "search/autocomplete/" + Uri.EscapeDataString(name.Trim()), ct);
        var games = SgdbQuery.ParseGames(json);
        return games.Count > 0 ? games[0] : null;
    }

    /// صفحة من الصور. يرمي HttpRequestException عند انقطاع الإنترنت أو رفض المفتاح.
    public virtual async Task<SgdbPage> GetImagesAsync(int gameId, SgdbArtType type, string? style, bool animated, int page, CancellationToken ct = default)
    {
        if (!IsConfigured) return new SgdbPage(Array.Empty<SgdbImage>(), 0);
        var json = await _http.GetStringAsync(ApiBase + SgdbQuery.BuildPath(gameId, type, style, animated, page), ct);
        return SgdbQuery.ParsePage(json, animated);
    }
}
