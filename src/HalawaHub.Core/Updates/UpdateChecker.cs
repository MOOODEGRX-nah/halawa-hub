using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using HalawaHub.Core;

namespace HalawaHub.Core.Updates;

/// <param name="IsReleasePage">
/// true يعني أن DownloadUrl صفحة الإصدار وليس ملفًا قابلًا للتثبيت التلقائي
/// (لا يوجد ملف مناسب لهذا النظام)، فيُفتح في المتصفح بدل تنزيله.
/// </param>
public record UpdateInfo(string LatestVersion, string DownloadUrl, bool IsNewer, string? Sha256 = null, bool IsReleasePage = false);

public class UpdateChecker
{
    /// <summary>اسم ملف التحديث على ويندوز كما ينشره build.yml.</summary>
    public const string WindowsAssetName = "Halawa-Hub.zip";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <returns>null يعني فشل الفحص (لا إنترنت، خطأ GitHub...) وليس "لا يوجد تحديث".</returns>
    public virtual async Task<UpdateInfo?> CheckForUpdateAsync()
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
            return ParseRelease(json, AppInfo.Version, GetAssetNameForCurrentOs());
        }
        catch (Exception ex)
        {
            Log.Error("failed to check updates", ex);
            return null;
        }
    }

    /// <summary>null يعني أن هذا النظام لا يملك ملف تحديث تلقائي بعد.</summary>
    internal static string? GetAssetNameForCurrentOs() =>
        OperatingSystem.IsWindows() ? WindowsAssetName : null;

    /// <summary>
    /// يقرأ رد GitHub ويختار الملف بالاسم لا بالترتيب: الإصدار يحتوي ملفات أخرى
    /// (SHA256SUMS.txt مثلًا) وأي تغيير في ترتيبها كان سيُنزّل الملف الخطأ.
    /// إذا لم يوجد الملف نرجع لصفحة الإصدار.
    /// </summary>
    internal static UpdateInfo? ParseRelease(string json, string currentVersion, string? assetName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var latestVersion = tagName.TrimStart('v', 'V');
        if (string.IsNullOrEmpty(latestVersion)) return null;

        string? downloadUrl = null;
        string? sha256 = null;

        if (assetName != null
            && root.TryGetProperty("assets", out var assets)
            && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!asset.TryGetProperty("browser_download_url", out var u)) continue;

                downloadUrl = u.GetString();
                if (asset.TryGetProperty("digest", out var digest))
                    sha256 = digest.GetString()?.Replace("sha256:", "");
                break;
            }
        }

        var isReleasePage = string.IsNullOrEmpty(downloadUrl);
        if (isReleasePage)
        {
            downloadUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
            sha256 = null;
        }

        return new UpdateInfo(
            latestVersion,
            downloadUrl ?? "",
            IsVersionNewer(latestVersion, currentVersion),
            sha256,
            isReleasePage);
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

    private static int[] ParseVersion(string version) =>
        version.Split('-')[0].Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
}
