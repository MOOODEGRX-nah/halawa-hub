using System.Threading.Tasks;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.Core.Updates;
using Moq;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات UpdateService — فحص التحديث مع mock UpdateChecker.
/// </summary>
public class UpdateServiceTests
{
    // الرسائل مترجمة، وهذه الاختبارات تتحقق من النص العربي
    public UpdateServiceTests() => Loc.Instance.Language = Loc.Arabic;

    [Fact]
    public async Task CheckForUpdate_NoUpdate_ReturnsHasUpdateFalse()
    {
        // Arrange
        var mockChecker = new Mock<UpdateChecker>();
        mockChecker.Setup(c => c.CheckForUpdateAsync())
            .ReturnsAsync(new UpdateInfo("0.0.10.45", "", false));

        var service = new UpdateService(mockChecker.Object);

        // Act
        var result = await service.CheckForUpdateAsync();

        // Assert
        result.HasUpdate.Should().BeFalse();
        result.Message.Should().Contain("محدّث");
    }

    [Fact]
    public async Task CheckForUpdate_CheckerFailed_ReportsFailure_NotUpToDate()
    {
        // انحدار: فشل الفحص كان يظهر للمستخدم "البرنامج محدّث"
        var mockChecker = new Mock<UpdateChecker>();
        mockChecker.Setup(c => c.CheckForUpdateAsync()).ReturnsAsync((UpdateInfo?)null);

        var result = await new UpdateService(mockChecker.Object).CheckForUpdateAsync();

        result.Failed.Should().BeTrue();
        result.HasUpdate.Should().BeFalse();
        result.Message.Should().NotContain("محدّث");
    }

    [Fact]
    public async Task CheckForUpdate_UpToDate_IsNotAFailure()
    {
        var mockChecker = new Mock<UpdateChecker>();
        mockChecker.Setup(c => c.CheckForUpdateAsync())
            .ReturnsAsync(new UpdateInfo("0.0.10.45", "", false));

        var result = await new UpdateService(mockChecker.Object).CheckForUpdateAsync();

        result.Failed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_ReleasePageFallback_IsPassedThrough()
    {
        var mockChecker = new Mock<UpdateChecker>();
        mockChecker.Setup(c => c.CheckForUpdateAsync())
            .ReturnsAsync(new UpdateInfo("9.0.0.0", "https://github.com/o/r/releases/tag/v9", true, null, IsReleasePage: true));

        var result = await new UpdateService(mockChecker.Object).CheckForUpdateAsync();

        result.IsReleasePage.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdate_NewerVersion_ReturnsUpdateInfo()
    {
        // Arrange
        var mockChecker = new Mock<UpdateChecker>();
        mockChecker.Setup(c => c.CheckForUpdateAsync())
            .ReturnsAsync(new UpdateInfo("0.0.10.47", "https://example.com/update.zip", true, "sha256"));

        var service = new UpdateService(mockChecker.Object);

        // Act
        var result = await service.CheckForUpdateAsync();

        // Assert
        result.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be("0.0.10.47");
        result.DownloadUrl.Should().Be("https://example.com/update.zip");
    }
}
