using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HalawaHub.Core.News;

public record NewsItem(string GameName, string Title, string Url, DateTime Date);

/// <summary>
/// يجيب آخر أخبار لعبة Steam معيّنة من API ستيم العام المجاني
/// (ISteamNews) — بدون أي مفتاح API.
/// </summary>
public class SteamNewsClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<List<NewsItem>> GetNewsForAppAsync(string gameName, string appId, int count = 1)
    {
        var results = new List<NewsItem>();

        try
        {
            var url = $"https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/" +
                      $"?appid={appId}&count={count}&maxlength=200&format=json";

            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("appnews", out var appNews) ||
                !appNews.TryGetProperty("newsitems", out var items))
                return results;

            foreach (var item in items.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var contentUrl = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                var dateUnix = item.TryGetProperty("date", out var d) ? d.GetInt64() : 0;

                if (string.IsNullOrEmpty(title)) continue;

                results.Add(new NewsItem(gameName, title, contentUrl,
                    DateTimeOffset.FromUnixTimeSeconds(dateUnix).UtcDateTime));
            }
        }
        catch
        {
            // فشل الجلب (لا إنترنت، ستيم مو متاح...) — نرجع قائمة فاضية بهدوء
        }

        return results;
    }
}
