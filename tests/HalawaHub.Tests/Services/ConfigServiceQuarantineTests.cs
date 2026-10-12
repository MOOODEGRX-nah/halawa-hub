using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests.Services;

public class ConfigServiceQuarantineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _path;

    public ConfigServiceQuarantineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            "halawahub-b04-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _path = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { /* test cleanup best-effort */ }
    }

    [Fact]
    public void MissingFile_CreatesDefault()
    {
        var cfg = ConfigService.LoadFrom(_path);
        cfg.Should().NotBeNull();
        cfg.SteamGridDbApiKey.Should().Be("");
        File.Exists(_path).Should().BeTrue("LoadFrom should create a default file");
    }

    [Fact]
    public void CorruptJson_QuarantinesAndCreatesDefault_E5()
    {
        // سيناريو E5: فاصلة/قوس ناقص → JsonException
        File.WriteAllText(_path, "{ \"SteamGridDbApiKey\": \"my-secret-key\" ");

        var cfg = ConfigService.LoadFrom(_path);

        cfg.Should().NotBeNull();
        cfg.SteamGridDbApiKey.Should().Be("", "the default replaces the corrupt file");
        File.Exists(_path).Should().BeTrue("a clean default was written");

        var quarantined = Directory.GetFiles(_tempDir, "config.json.corrupt-*");
        quarantined.Should().HaveCount(1, "the corrupt file must be preserved");
        File.ReadAllText(quarantined[0]).Should().Contain("my-secret-key",
            "the original (broken) content is kept for manual recovery");
    }

    [Fact]
    public void ValidJson_PreservesValues()
    {
        var original = new AppConfig { SteamGridDbApiKey = "real-key", LastSeenVersion = "0.0.10.50" };
        File.WriteAllText(_path, JsonSerializer.Serialize(original));

        var loaded = ConfigService.LoadFrom(_path);

        loaded.SteamGridDbApiKey.Should().Be("real-key");
        loaded.LastSeenVersion.Should().Be("0.0.10.50");
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var cfg = new AppConfig { SteamGridDbApiKey = "round-trip", LastSeenVersion = "0.0.10.54" };
        ConfigService.SaveTo(_path, cfg);

        var loaded = ConfigService.LoadFrom(_path);

        loaded.SteamGridDbApiKey.Should().Be("round-trip");
        loaded.LastSeenVersion.Should().Be("0.0.10.54");
    }

    [Fact]
    public void EmptyJson_QuarantinesAndCreatesDefault()
    {
        File.WriteAllText(_path, "");

        var cfg = ConfigService.LoadFrom(_path);

        cfg.Should().NotBeNull();
        Directory.GetFiles(_tempDir, "config.json.corrupt-*").Should().HaveCount(1);
    }
}
