using System.Net.Http;
using System.Text.Json;
using HalawaHub.Core.Models;

namespace HalawaHub.Core.Store;

/// <summary>
/// ألعاب Steam المملوكة عبر الواجهة الرسمية IPlayerService/GetOwnedGames (موثّقة من Valve،
/// تحتاج مفتاح Web API يملكه صاحب الحساب و SteamID64). ميزة اختيارية: بلا مفتاح لا يُطلب شيء.
/// الرد: {"response":{"game_count":N,"games":[{"appid":570,"name":"Dota 2","playtime_forever":123}]}}
/// </summary>
public static class SteamOwnedGames
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// null = فشل الطلب (مفتاح خاطئ، ملف شخصي خاص، لا إنترنت)
    public static async Task<string?> FetchJsonAsync(string apiKey, string steamId64)
    {
        try
        {
            var url = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?format=json&include_appinfo=1&include_played_free_games=1" +
                      $"&key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}";
            using var response = await Http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                // لا نكتب الرابط في السجل: فيه المفتاح
                Log.Info($"Steam owned games: HTTP {(int)response.StatusCode}");
                return null;
            }
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Log.Info("Steam owned games: request failed (" + ex.GetType().Name + ")");
            return null;
        }
    }

    public static List<GameInfo> Parse(string? json)
    {
        var result = new List<GameInfo>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("response", out var r) || !r.TryGetProperty("games", out var games)
                || games.ValueKind != JsonValueKind.Array) return result;

            foreach (var g in games.EnumerateArray())
            {
                if (!g.TryGetProperty("appid", out var id) || id.ValueKind != JsonValueKind.Number) continue;
                var name = g.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;
                var appId = id.GetInt64().ToString();
                result.Add(new GameInfo
                {
                    Id = appId,
                    Name = name,
                    Platform = "Steam",
                    InstallPath = "",
                    ExecutablePath = $"steam://install/{appId}",
                    CoverImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
                    CoverImageUrlFallback = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                    IsInstalled = false
                });
            }
        }
        catch (Exception ex)
        {
            Log.Info("Steam owned games: unexpected format (" + ex.Message + ")");
            result.Clear();
        }
        return result;
    }
}
