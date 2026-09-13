using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using HalawaHub.Core;

namespace HalawaHub.Core.Updates;

public record UpdateInfo(string LatestVersion, string DownloadUrl, bool IsNewer, string? Sha256 = null);

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
            string? sha256 = null;
            if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                var asset = assets[0];
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                if (asset.TryGetProperty("digest", out var digest))
                    sha256 = digest.GetString()?.Replace("sha256:", "");
            }

            downloadUrl ??= root.TryGetProperty("html_url", out var h) ? h.GetString() : null;

            return new UpdateInfo(latestVersion, downloadUrl ?? "", IsVersionNewer(latestVersion, AppInfo.Version), sha256);
        }
        catch (Exception ex)
        {
            Log.Error("failed to check updates", ex);
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

    private static int[] ParseVersion(string version) =>
        version.Split('-')[0].Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
}
