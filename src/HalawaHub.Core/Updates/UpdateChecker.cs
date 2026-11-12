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
/// <param name="ChecksumsUrl">رابط SHA256SUMS.txt في الإصدار، احتياطي لو لم يذكر GitHub هاش الملف.</param>
/// <param name="ReleasePageUrl">صفحة الإصدار، نرجع إليها إذا تعذر التحقق من الملف.</param>
public record UpdateInfo(
    string LatestVersion,
    string DownloadUrl,
    bool IsNewer,
    string? Sha256 = null,
    bool IsReleasePage = false,
    string? ChecksumsUrl = null,
    string? ReleasePageUrl = null);

public class UpdateChecker
{
    /// <summary>اسم ملف التحديث على ويندوز كما ينشره build.yml.</summary>
    public const string WindowsAssetName = "Halawa-Hub.zip";

    /// <summary>ملف الهاشات الموحّد لكل ملفات الإصدار.</summary>
    public const string ChecksumsAssetName = "SHA256SUMS.txt";

    /// <summary>
    /// يوجّه الفحص لمصدر آخر بدل GitHub، لتجربة التحديث من إصدار اختباري محلي
    /// (مثل HALAWAHUB_DATA_DIR: من يتحكم ببيئة العملية يتحكم بها كلها أصلًا).
    /// </summary>
    public const string FeedOverrideVariable = "HALAWAHUB_UPDATE_API_URL";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <returns>null يعني فشل الفحص (لا إنترنت، خطأ GitHub...) وليس "لا يوجد تحديث".</returns>
    public virtual async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var feedOverride = Environment.GetEnvironmentVariable(FeedOverrideVariable);
            if (!string.IsNullOrWhiteSpace(feedOverride))
                Log.Info($"update feed overridden: {feedOverride}");

            var url = !string.IsNullOrWhiteSpace(feedOverride)
                ? feedOverride
                : $"https://api.github.com/repos/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/releases/latest";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Halawa-Hub", AppInfo.Version));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var assetName = GetAssetNameForCurrentOs();
            var info = ParseRelease(json, AppInfo.Version, assetName);

            // لا يُثبَّت ملف بلا هاش على أي نظام: بدونه يصبح التحديث إشعارًا وصفحة الإصدار
            if (info != null)
                info = await ResolveChecksumAsync(info, assetName);

            return info;
        }
        catch (Exception ex)
        {
            Log.Error("failed to check updates", ex);
            return null;
        }
    }

    /// <summary>null يعني أن هذا النظام لا يملك ملف تحديث تلقائي بعد.</summary>
    internal static string? GetAssetNameForCurrentOs() => InstallInfo.AssetNameFor(InstallInfo.Current);

    private static async Task<UpdateInfo> ResolveChecksumAsync(UpdateInfo info, string? assetName)
    {
        if (info.IsReleasePage || !string.IsNullOrEmpty(info.Sha256)) return info;

        string? sums = null;
        if (!string.IsNullOrEmpty(info.ChecksumsUrl))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, info.ChecksumsUrl);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Halawa-Hub", AppInfo.Version));
                using var response = await Http.SendAsync(request);
                if (response.IsSuccessStatusCode) sums = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                Log.Error("could not download SHA256SUMS.txt", ex);
            }
        }

        return ApplyChecksums(info, sums, assetName);
    }

    /// <summary>الهاش من SHA256SUMS.txt، وإذا لم يوجد فالتحديث إشعار وصفحة الإصدار فقط.</summary>
    internal static UpdateInfo ApplyChecksums(UpdateInfo info, string? sumsFile, string? assetName)
    {
        var hash = FindChecksum(sumsFile, assetName);
        if (!string.IsNullOrEmpty(hash)) return info with { Sha256 = hash };

        return info with { DownloadUrl = info.ReleasePageUrl ?? "", IsReleasePage = true };
    }

    /// <summary>يقرأ صيغة sha256sum: "hash  name" أو "hash *name".</summary>
    internal static string? FindChecksum(string? sumsFile, string? assetName)
    {
        if (string.IsNullOrEmpty(sumsFile) || string.IsNullOrEmpty(assetName)) return null;

        foreach (var line in sumsFile.Split('\n'))
        {
            var parts = line.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;

            if (string.Equals(parts[1].TrimStart('*'), assetName, StringComparison.OrdinalIgnoreCase))
                return parts[0];
        }
        return null;
    }

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
        string? checksumsUrl = null;

        if (root.TryGetProperty("assets", out var assets)
            && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!asset.TryGetProperty("browser_download_url", out var u)) continue;

                if (string.Equals(name, ChecksumsAssetName, StringComparison.OrdinalIgnoreCase))
                    checksumsUrl = u.GetString();

                if (assetName == null || downloadUrl != null) continue;
                if (!string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase)) continue;

                downloadUrl = u.GetString();
                if (asset.TryGetProperty("digest", out var digest))
                    sha256 = digest.GetString()?.Replace("sha256:", "");
            }
        }

        var releasePage = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
        var isReleasePage = string.IsNullOrEmpty(downloadUrl);
        if (isReleasePage)
        {
            downloadUrl = releasePage;
            sha256 = null;
        }

        return new UpdateInfo(
            latestVersion,
            downloadUrl ?? "",
            IsVersionNewer(latestVersion, currentVersion),
            sha256,
            isReleasePage,
            checksumsUrl,
            releasePage);
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
