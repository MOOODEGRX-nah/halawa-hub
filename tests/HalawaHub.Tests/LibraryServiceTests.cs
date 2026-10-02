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
/// اختبارات LibraryService — فحص المكتبات مع mock providers.
/// </summary>
public class LibraryServiceTests
{
    private readonly LibraryService _service = new();

    [Fact]
    public void ScanAllLibraries_EmptyProviders_ReturnsEmpty()
    {
        // Act
        var result = _service.ScanAllLibraries(
            new List<IGameLibraryProvider>(),
            new List<IGameTool>());

        // Assert
        result.Games.Should().BeEmpty();
        result.PlatformCounts.Should().HaveCount(5); // 5 منصات مدعومة
    }

    [Fact]
    public void ScanAllLibraries_WithSteamProvider_ReturnsGames()
    {
        // Arrange
        var mockProvider = new Mock<IGameLibraryProvider>();
        mockProvider.Setup(p => p.IsAvailable()).Returns(true);
        mockProvider.Setup(p => p.PlatformName).Returns("Steam");
        mockProvider.Setup(p => p.ScanLibrary()).Returns(new List<GameInfo>
        {
            new() { Id = "730", Name = "CS2", Platform = "Steam" }
        });

        // Act
        var result = _service.ScanAllLibraries(
            new[] { mockProvider.Object },
            new List<IGameTool>());

        // Assert
        result.Games.Should().HaveCount(1);
        result.PlatformCounts["Steam"].Should().Be(1);
    }

    [Fact]
    public void ScanAllLibraries_DuplicateGames_Deduped()
    {
        // Arrange
        var mockProvider1 = new Mock<IGameLibraryProvider>();
        mockProvider1.Setup(p => p.IsAvailable()).Returns(true);
        mockProvider1.Setup(p => p.PlatformName).Returns("Steam");
        mockProvider1.Setup(p => p.ScanLibrary()).Returns(new List<GameInfo>
        {
            new() { Id = "730", Name = "CS2", Platform = "Steam" }
        });

        var mockProvider2 = new Mock<IGameLibraryProvider>();
        mockProvider2.Setup(p => p.IsAvailable()).Returns(true);
        mockProvider2.Setup(p => p.PlatformName).Returns("Steam");
        mockProvider2.Setup(p => p.ScanLibrary()).Returns(new List<GameInfo>
        {
            new() { Id = "730", Name = "CS2 Duplicate", Platform = "Steam" } // نفس ID
        });

        // Act
        var result = _service.ScanAllLibraries(
            new[] { mockProvider1.Object, mockProvider2.Object },
            new List<IGameTool>());

        // Assert
        result.Games.Should().HaveCount(1); // Deduplication
    }

    [Fact]
    public void ScanAllLibraries_UnavailableProvider_Skipped()
    {
        // Arrange
        var mockProvider = new Mock<IGameLibraryProvider>();
        mockProvider.Setup(p => p.IsAvailable()).Returns(false);
        mockProvider.Setup(p => p.PlatformName).Returns("Steam");

        // Act
        var result = _service.ScanAllLibraries(
            new[] { mockProvider.Object },
            new List<IGameTool>());

        // Assert
        result.Games.Should().BeEmpty();
    }
}
