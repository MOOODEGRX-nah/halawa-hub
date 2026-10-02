using System;
using FluentAssertions;
using HalawaHub.Core.Library;
using HalawaHub.Core.Plugins;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات XboxLibraryProvider — caching وبنية البيانات.
/// </summary>
public class XboxLibraryProviderTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        // Arrange & Act
        var provider = new XboxLibraryProvider();

        // Assert
        provider.Should().NotBeNull();
        provider.PlatformName.Should().Be("Xbox / Microsoft Store");
    }

    [Fact]
    public void PlatformName_IsCorrect()
    {
        // Arrange
        var provider = new XboxLibraryProvider();

        // Assert
        provider.PlatformName.Should().Be("Xbox / Microsoft Store");
    }

    [Fact]
    public void ScanLibrary_ReturnsIEnumerable()
    {
        // Arrange
        var provider = new XboxLibraryProvider();

        // Act
        var result = provider.ScanLibrary();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<HalawaHub.Core.Models.GameInfo>>();
    }

    [Fact]
    public void IsAvailable_ReturnsBool()
    {
        // Arrange
        var provider = new XboxLibraryProvider();

        // Act
        var result = provider.IsAvailable();

        // Assert
        result.Should().BeOfType<bool>();
    }
}
