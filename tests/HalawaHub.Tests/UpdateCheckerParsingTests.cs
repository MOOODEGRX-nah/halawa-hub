using FluentAssertions;
using HalawaHub.Core.Updates;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// انحدار: كان التحديث يأخذ assets[0]، فأي تغيير في ترتيب ملفات الإصدار
/// كان سيُنزّل SHA256SUMS.txt بدل الملف المضغوط.
/// </summary>
public class UpdateCheckerParsingTests
{
    private const string ReleasePage = "https://github.com/o/r/releases/tag/v9.0.0.0";

    private static string Release(params (string Name, string Url, string? Digest)[] assets)
    {
        var items = string.Join(",", assets.Select(a =>
            $$"""{"name":"{{a.Name}}","browser_download_url":"{{a.Url}}"{{(a.Digest == null ? "" : $",\"digest\":\"sha256:{a.Digest}\"")}}}"""));
        return $$"""{"tag_name":"v9.0.0.0","html_url":"{{ReleasePage}}","assets":[{{items}}]}""";
    }

    [Fact]
    public void Parse_PicksZipByName_WhenNotFirstAsset()
    {
        var json = Release(
            ("SHA256SUMS.txt", "https://x/SHA256SUMS.txt", "aaa"),
            ("Halawa-Hub-linux.tar.gz", "https://x/linux.tar.gz", "bbb"),
            ("Halawa-Hub.zip", "https://x/Halawa-Hub.zip", "ccc"));

        var info = UpdateChecker.ParseRelease(json, "1.0.0.0", UpdateChecker.WindowsAssetName);

        info!.DownloadUrl.Should().Be("https://x/Halawa-Hub.zip");
        info.Sha256.Should().Be("ccc", "the digest must belong to the chosen asset");
        info.IsReleasePage.Should().BeFalse();
        info.IsNewer.Should().BeTrue();
    }

    [Fact]
    public void Parse_NameMatch_IsCaseInsensitive()
    {
        var json = Release(("halawa-hub.ZIP", "https://x/z.zip", null));

        UpdateChecker.ParseRelease(json, "1.0.0.0", UpdateChecker.WindowsAssetName)!
            .DownloadUrl.Should().Be("https://x/z.zip");
    }

    [Fact]
    public void Parse_ZipMissing_FallsBackToReleasePage()
    {
        var json = Release(("SHA256SUMS.txt", "https://x/SHA256SUMS.txt", "aaa"));

        var info = UpdateChecker.ParseRelease(json, "1.0.0.0", UpdateChecker.WindowsAssetName);

        info!.DownloadUrl.Should().Be(ReleasePage);
        info.IsReleasePage.Should().BeTrue();
        info.Sha256.Should().BeNull();
    }

    [Fact]
    public void Parse_OsWithoutAsset_FallsBackToReleasePage()
    {
        var json = Release(("Halawa-Hub.zip", "https://x/Halawa-Hub.zip", "ccc"));

        var info = UpdateChecker.ParseRelease(json, "1.0.0.0", assetName: null);

        info!.DownloadUrl.Should().Be(ReleasePage);
        info.IsReleasePage.Should().BeTrue();
    }

    [Fact]
    public void Parse_SameVersion_IsNotNewer()
    {
        var json = Release(("Halawa-Hub.zip", "https://x/z.zip", null));

        UpdateChecker.ParseRelease(json, "9.0.0.0", UpdateChecker.WindowsAssetName)!
            .IsNewer.Should().BeFalse();
    }

    [Fact]
    public void Parse_NoTag_ReturnsNull()
    {
        UpdateChecker.ParseRelease("""{"assets":[]}""", "1.0.0.0", UpdateChecker.WindowsAssetName)
            .Should().BeNull();
    }
}
