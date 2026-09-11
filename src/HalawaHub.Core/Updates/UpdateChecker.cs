using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace HalawaHub.Core.Updates;

public record UpdateInfo(string LatestVersion, string DownloadUrl, bool IsNewer);

/// <summary>
/// يفحص إصدارات GitHub Releases الخاصة بالمستودع ويقارنها بالإصدار الحالي.
/// يفشل بصمت (يرجع null) عند أي مشكلة اتصال — عشان المستخدم بدون إنترنت
/// ما يشوف أي خطأ مزعج، بس ما يظهر له إشعار تحديث وخلاص.
/// </summary>
public class UpdateChecker
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/releases/latest";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Halawa-Hub", AppInfo.Version));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var latestVersion = tagName.TrimStart('v', 'V');
            if (string.IsNullOrEmpty(latestVersion)) return null;

            string? downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                downloadUrl = assets[0].GetProperty("browser_download_url").GetString();

            downloadUrl ??= root.TryGetProperty("html_url", out var h) ? h.GetString() : null;

            return new UpdateInfo(latestVersion, downloadUrl ?? "", IsVersionNewer(latestVersion, AppInfo.Version));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsVersionNewer(string latest, string current)
    {
        try
        {
            var latestParts = ParseVersion(latest);
            var currentParts = ParseVersion(current);

            for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
            {
                var l = i < latestParts.Length ? latestParts[i] : 0;
                var c = i < currentParts.Length ? currentParts[i] : 0;
                if (l != c) return l > c;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    // يشيل أي لاحقة زي "-beta" قبل مقارنة الأرقام
    private static int[] ParseVersion(string version) =>
        version.Split('-')[0].Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
}
