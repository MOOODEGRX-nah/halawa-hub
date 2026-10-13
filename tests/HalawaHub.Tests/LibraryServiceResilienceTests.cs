using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using Moq;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات انحدار لـ B07/E3/E4 — تحصين مزودي المكتبات:
/// - provider يرمي exception لا يكسر بقية المسح
/// - provider يرمي في منتصف enumeration نحتفظ بالنتائج الجزئية
/// - IsAvailable() يرمي لا يكسر
/// - PlatformCounts تُشتق من البيانات (تدعم منصات الإضافات)
/// - null/empty GameInfo يُتخطى
/// </summary>
public class LibraryServiceResilienceTests
{
    private readonly LibraryService _service = new();

    [Fact]
    public void ThrowingProvider_DoesNotBreakOtherProviders()
    {
        // Arrange: provider1 يعمل، provider2 يرمي
        var provider1 = new Mock<IGameLibraryProvider>();
        provider1.Setup(p => p.IsAvailable()).Returns(true);
        provider1.Setup(p => p.PlatformName).Returns("Steam");
        provider1.Setup(p => p.ScanLibrary()).Returns(new List<GameInfo>
        {
            new() { Id = "730", Name = "CS2", Platform = "Steam" }
        });

        var provider2 = new Mock<IGameLibraryProvider>();
        provider2.Setup(p => p.IsAvailable()).Returns(true);
        provider2.Setup(p => p.PlatformName).Returns("Epic");
        provider2.Setup(p => p.ScanLibrary()).Throws(new InvalidOperationException("Epic failed"));

        // Act
        var result = _service.ScanAllLibraries(
            new[] { provider1.Object, provider2.Object },
            new List<IGameTool>());

        // Assert: Steam نجح رغم فشل Epic
        result.Games.Should().HaveCount(1);
        result.Games[0].Platform.Should().Be("Steam");
    }

    [Fact]
    public void ThrowingMidEnumeration_KeepsPartialResults()
    {
        // Arrange: provider يرمي بعد yield لعبتين
        var provider = new Mock<IGameLibraryProvider>();
        provider.Setup(p => p.IsAvailable()).Returns(true);
        provider.Setup(p => p.PlatformName).Returns("Steam");
        provider.Setup(p => p.ScanLibrary()).Returns(ThrowingAfterTwo());

        // Act
        var result = _service.ScanAllLibraries(
            new[] { provider.Object },
            new List<IGameTool>());

        // Assert: نحتفظ بالعنصرين الذين جُمعا قبل الاستثناء
        result.Games.Should().HaveCount(2);
    }

    [Fact]
    public void UnavailableProviderThatThrows_IsSkipped()
    {
        // Arrange: IsAvailable() يرمي exception
        var provider = new Mock<IGameLibraryProvider>();
        provider.Setup(p => p.IsAvailable()).Throws(new UnauthorizedAccessException("Registry locked"));
        provider.Setup(p => p.PlatformName).Returns("GOG");

        // Act
        var result = _service.ScanAllLibraries(
            new[] { provider.Object },
            new List<IGameTool>());

        // Assert: المسح يكمل بدون exception
        result.Games.Should().BeEmpty();
    }

    [Fact]
    public void PlatformCounts_DerivedFromData()
    {
        // Arrange: mock provider بمنصة "Itch.io" (منصة إضافية غير مدمجة)
        var provider = new Mock<IGameLibraryProvider>();
        provider.Setup(p => p.IsAvailable()).Returns(true);
        provider.Setup(p => p.PlatformName).Returns("Itch.io");
        provider.Setup(p => p.ScanLibrary()).Returns(new List<GameInfo>
        {
            new() { Id = "game1", Name = "Indie Game", Platform = "Itch.io" },
            new() { Id = "game2", Name = "Another Indie", Platform = "Itch.io" }
        });

        // Act
        var result = _service.ScanAllLibraries(
            new[] { provider.Object },
            new List<IGameTool>());

        // Assert: PlatformCounts تظهر "Itch.io" تلقائيًا (ليست مدمجة في 5 منصات)
        result.PlatformCounts.Should().ContainKey("Itch.io");
        result.PlatformCounts["Itch.io"].Should().Be(2);
    }

    [Fact]
    public void NullGameInfo_IsSkipped()
    {
        // Arrange: provider يرجع null في منتصف القائمة
        var provider = new Mock<IGameLibraryProvider>();
        provider.Setup(p => p.IsAvailable()).Returns(true);
        provider.Setup(p => p.PlatformName).Returns("Steam");
        provider.Setup(p => p.ScanLibrary()).Returns(new List<GameInfo?>
        {
            new() { Id = "730", Name = "CS2", Platform = "Steam" },
            null,
            new() { Id = "570", Name = "Dota 2", Platform = "Steam" }
        }!);

        // Act
        var result = _service.ScanAllLibraries(
            new[] { provider.Object },
            new List<IGameTool>());

        // Assert: null يُتخطى
        result.Games.Should().HaveCount(2);
    }

    private static IEnumerable<GameInfo> ThrowingAfterTwo()
    {
        yield return new GameInfo { Id = "730", Name = "CS2", Platform = "Steam" };
        yield return new GameInfo { Id = "570", Name = "Dota 2", Platform = "Steam" };
        throw new InvalidOperationException("Mid-enumeration failure");
    }
}
