using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HalawaHub.Core.Updates;

/// <summary>
/// يجيب قائمة التغييرات لإصدار معيّن من ملف CHANGELOG.json بجذر المستودع،
/// عشان نعرض نافذة "ما الجديد" بعد كل تحديث بدون الحاجة لأي خادم خاص.
///
/// تحصين B09a:
///   - يقرأ من tag الإصدار (v{version}) لا من main المتحرك
///   - URL builder منفصل كـ internal للاختبار
///   - Log.Error عند فشل الجلب (كان catch صامت)
/// </summary>
public static class ChangelogService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// يبني URL للـ CHANGELOG من tag الإصدار (لا من main المتحرك).
    /// internal للاختبار.
    /// </summary>
    internal static string BuildChangelogUrl(string owner, string repo, string version)
    {
        return $"https://raw.githubusercontent.com/{owner}/{repo}/v{version}/CHANGELOG.json";
    }

    public static async Task<List<string>> GetChangesForVersionAsync(string owner, string repo, string version)
    {
        try
        {
            var url = BuildChangelogUrl(owner, repo, version);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("HalawaHub/1.0");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"ChangelogService: HTTP {(int)response.StatusCode} for {url}");
                return new List<string>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);

            if (dict != null && dict.TryGetValue(version, out var changes))
                return changes;

            Log.Error($"ChangelogService: version {version} not found in CHANGELOG.json");
        }
        catch (Exception ex)
        {
            // فشل الجلب (لا إنترنت، الملف مو موجود بعد...) — نسجل الخطأ
            Log.Error($"ChangelogService: failed to fetch changelog for v{version}", ex);
        }

        return new List<string>();
    }
}
