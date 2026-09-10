using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace HalawaHub.Core.Covers;

/// <summary>
/// يجيب أغلفة الألعاب من SteamGridDB (قاعدة بيانات مجتمعية مجانية تغطي
/// كل المنصات تقريبًا) بالاسم. يحتاج مفتاح API مجاني من المستخدم
/// (steamgriddb.com/profile/preferences) — بدون مفتاح، IsConfigured تكون
/// false ولا يحاول يتصل بأي شيء.
/// </summary>
public class SteamGridDbClient
{
    private readonly HttpClient _http;

    public bool IsConfigured { get; }

    public SteamGridDbClient(string? apiKey)
    {
        IsConfigured = !string.IsNullOrWhiteSpace(apiKey);

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (IsConfigured)
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string?> FindCoverUrlAsync(string gameName)
    {
        if (!IsConfigured) return null;

        try
        {
            var searchUrl = $"https://www.steamgriddb.com/api/v2/search/autocomplete/{Uri.EscapeDataString(gameName)}";
            var searchJson = await _http.GetStringAsync(searchUrl);
            using var searchDoc = JsonDocument.Parse(searchJson);

            if (!searchDoc.RootElement.TryGetProperty("data", out var results) || results.GetArrayLength() == 0)
                return null;

            var gameId = results[0].GetProperty("id").GetInt32();

            var gridsUrl = $"https://www.steamgriddb.com/api/v2/grids/game/{gameId}?dimensions=600x900";
            var gridsJson = await _http.GetStringAsync(gridsUrl);
            using var gridsDoc = JsonDocument.Parse(gridsJson);

            if (!gridsDoc.RootElement.TryGetProperty("data", out var grids) || grids.GetArrayLength() == 0)
                return null;

            return grids[0].GetProperty("url").GetString();
        }
        catch
        {
            // فشل البحث (لعبة غير موجودة بقاعدة البيانات، مشكلة اتصال...) — تجاهل
            return null;
        }
    }

    /// يتحقق إن المفتاح فعليًا صحيح ومقبول عند SteamGridDB.
    /// ملاحظة: مسار البحث (autocomplete) ما يتطلب توثيق أصلاً، فأي مفتاح
    /// (حتى غلط) يرجع نجاح منه — لذا نستخدم مسار الصور (grids) اللي فعليًا
    /// يرفض أي مفتاح خاطئ برمز 401/403.
    public async Task<bool> ValidateApiKeyAsync()
    {
        if (!IsConfigured) return false;

        try
        {
            using var response = await _http.GetAsync("https://www.steamgriddb.com/api/v2/grids/game/1");

            // مفتاح خاطئ يرجع 401 (Unauthorized) أو 403 (Forbidden) بالضبط —
            // أي رد ثاني (حتى لو 404 لعدم وجود اللعبة رقم 1) يعني المفتاح
            // نفسه انقبل والتوثيق نجح
            return response.StatusCode != System.Net.HttpStatusCode.Unauthorized
                && response.StatusCode != System.Net.HttpStatusCode.Forbidden;
        }
        catch
        {
            return false;
        }
    }
}
