using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HalawaHub.Core.Updates;

/// <summary>
/// يجيب قائمة التغييرات لإصدار معيّن من ملف CHANGELOG.json بجذر المستودع،
/// عشان نعرض نافذة "ما الجديد" بعد كل تحديث بدون الحاجة لأي خادم خاص.
/// </summary>
public static class ChangelogService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<List<string>> GetChangesForVersionAsync(string owner, string repo, string version)
    {
        try
        {
            var url = $"https://raw.githubusercontent.com/{owner}/{repo}/main/CHANGELOG.json";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("HalawaHub/1.0");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);

            if (dict != null && dict.TryGetValue(version, out var changes))
                return changes;
        }
        catch
        {
            // فشل الجلب (لا إنترنت، الملف مو موجود بعد...) — نتجاهل بهدوء
        }

        return new List<string>();
    }
}
