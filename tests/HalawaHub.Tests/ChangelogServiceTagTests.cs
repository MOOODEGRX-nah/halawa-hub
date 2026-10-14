using System.Reflection;
using FluentAssertions;
using HalawaHub.Core.Updates;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات انحدار لـ B09a: ChangelogService يقرأ من tag الإصدار لا من main المتحرك.
/// </summary>
public class ChangelogServiceTagTests
{
    [Fact]
    public void BuildChangelogUrl_UsesVersionTag_NotMainBranch()
    {
        // Arrange
        var owner = "MOOODEGRX-nah";
        var repo = "halawa-hub";
        var version = "0.0.10.56";

        // Act
        var url = ChangelogService.BuildChangelogUrl(owner, repo, version);

        // Assert: URL يجب أن يحتوي على /v0.0.10.56/ لا /main/
        url.Should().Contain("/v0.0.10.56/", "B09a: must read from version tag, not main branch");
        url.Should().NotContain("/main/", "B09a: main branch moves, users would miss changelogs");
        url.Should().Be("https://raw.githubusercontent.com/MOOODEGRX-nah/halawa-hub/v0.0.10.56/CHANGELOG.json");
    }

    [Fact]
    public void BuildChangelogUrl_HandlesDifferentVersions()
    {
        // Arrange & Act
        var url1 = ChangelogService.BuildChangelogUrl("owner", "repo", "1.2.3.4");
        var url2 = ChangelogService.BuildChangelogUrl("owner", "repo", "0.0.10.99");

        // Assert
        url1.Should().Contain("/v1.2.3.4/");
        url2.Should().Contain("/v0.0.10.99/");
    }

    [Fact]
    public void BuildChangelogUrl_IsInternal()
    {
        // sanity: نتأكد أن الـ method internal (للسماح بالاختبار دون كشفه كـ public API)
        var method = typeof(ChangelogService).GetMethod(
            "BuildChangelogUrl",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        method.Should().NotBeNull("BuildChangelogUrl must exist");
        method!.IsAssembly.Should().BeTrue("BuildChangelogUrl should be internal");
    }
}
