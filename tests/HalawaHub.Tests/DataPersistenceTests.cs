using System.IO;
using System.Text.Json;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.Core.Models;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات حماية بيانات المستخدم — تأكيد أن الملفات الحالية تشتغل.
/// Read-only tests (لا نعدل ملفات المستخدم).
/// </summary>
public class DataPersistenceTests
{
    [Fact]
    public void ConfigService_Load_ReturnsValidConfig()
    {
        // Arrange & Act
        var config = ConfigService.Load();

        // Assert
        config.Should().NotBeNull();
        config.SteamGridDbApiKey.Should().NotBeNull();
        config.LastSeenVersion.Should().NotBeNull();
    }

    [Fact]
    public void AppConfig_Serialization_RoundTrips()
    {
        // Arrange
        var original = new AppConfig
        {
            SteamGridDbApiKey = "test-key-12345",
            LastSeenVersion = "0.0.10.46"
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var loaded = JsonSerializer.Deserialize<AppConfig>(json);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.SteamGridDbApiKey.Should().Be(original.SteamGridDbApiKey);
        loaded.LastSeenVersion.Should().Be(original.LastSeenVersion);
    }

    [Fact]
    public void FavoritesService_FileExists_IsValidJson()
    {
        // Arrange
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HalawaHub", "favorites.json");

        // Act & Assert
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<string>>(json);
            items.Should().NotBeNull();
        }
    }

    [Fact]
    public void GameInfo_Serialization_RoundTrips()
    {
        // Arrange
        var original = new GameInfo
        {
            Id = "730",
            Name = "Counter-Strike 2",
            Platform = "Steam",
            IsInstalled = true
        };

        // Act
        var json = JsonSerializer.Serialize(original);
        var loaded = JsonSerializer.Deserialize<GameInfo>(json);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(original.Id);
        loaded.Name.Should().Be(original.Name);
        loaded.Platform.Should().Be(original.Platform);
    }
}
