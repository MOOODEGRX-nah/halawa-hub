using System;
using System.IO;
using FluentAssertions;
using HalawaHub.Core.Library;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// اختبارات محلّل Steam عبر مكتبة مزيفة على القرص (B22):
/// تعمل على أي نظام تشغيل لأنها filesystem خالص بدون Registry.
/// </summary>
public class SteamFakeLibraryTests : IDisposable
{
    private readonly string _root;

    public SteamFakeLibraryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "halawahub-steam-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "steamapps"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private void WriteManifest(string appId, string name, string installDir, bool createDir = true)
    {
        var acf = Path.Combine(_root, "steamapps", $"appmanifest_{appId}.acf");
        File.WriteAllText(acf,
            "\"AppState\"\n{\n" +
            $"\t\"appid\"\t\t\"{appId}\"\n" +
            $"\t\"name\"\t\t\"{name}\"\n" +
            $"\t\"installdir\"\t\t\"{installDir}\"\n" +
            "}\n");
        if (createDir)
            Directory.CreateDirectory(Path.Combine(_root, "steamapps", "common", installDir));
    }

    [Fact]
    public void ParsesManifest_AndMarksInstalled()
    {
        WriteManifest("730", "Counter-Strike 2", "cs2");

        var games = SteamLibraryProvider.ScanFromSteamPath(_root);

        games.Should().HaveCount(1);
        games[0].Id.Should().Be("730");
        games[0].Name.Should().Be("Counter-Strike 2");
        games[0].Platform.Should().Be("Steam");
        games[0].IsInstalled.Should().BeTrue();
        games[0].ExecutablePath.Should().Be("steam://rungameid/730");
    }

    [Fact]
    public void ToolPackages_AreFiltered()
    {
        WriteManifest("228980", "Steamworks Common Redistributables", "SteamworksShared");
        WriteManifest("1070560", "Steam Linux Runtime", "SteamLinuxRuntime");
        WriteManifest("730", "CS2", "cs2");

        var games = SteamLibraryProvider.ScanFromSteamPath(_root);

        games.Should().HaveCount(1);
        games[0].Id.Should().Be("730");
    }

    [Fact]
    public void MissingAppId_FallsBackToFilename()
    {
        var acf = Path.Combine(_root, "steamapps", "appmanifest_4000.acf");
        File.WriteAllText(acf,
            "\"AppState\"\n{\n\t\"name\"\t\t\"Portal\"\n\t\"installdir\"\t\t\"portal\"\n}\n");
        Directory.CreateDirectory(Path.Combine(_root, "steamapps", "common", "portal"));

        var games = SteamLibraryProvider.ScanFromSteamPath(_root);

        games.Should().HaveCount(1);
        games[0].Id.Should().Be("4000", "deterministic id from filename, not Guid");
    }

    [Fact]
    public void GarbageManifest_IsSkipped_NotFatal()
    {
        WriteManifest("730", "CS2", "cs2");
        File.WriteAllText(Path.Combine(_root, "steamapps", "appmanifest_9999.acf"),
            "this is not a valid acf at all {{{");

        var games = SteamLibraryProvider.ScanFromSteamPath(_root);

        games.Should().HaveCount(1, "corrupt manifest must not hide the library");
    }

    [Fact]
    public void DuplicateAppId_AcrossLibraries_Deduped()
    {
        WriteManifest("730", "CS2", "cs2");

        var second = Path.Combine(_root, "secondlib");
        Directory.CreateDirectory(Path.Combine(second, "steamapps", "common", "cs2"));
        File.WriteAllText(Path.Combine(second, "steamapps", "appmanifest_730.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"730\"\n\t\"name\"\t\t\"CS2\"\n\t\"installdir\"\t\t\"cs2\"\n}\n");

        File.WriteAllText(Path.Combine(_root, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n" +
            $"\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{_root}\"\n\t}}\n" +
            $"\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{second}\"\n\t}}\n" +
            "}\n");

        var games = SteamLibraryProvider.ScanFromSteamPath(_root);

        games.Should().HaveCount(1, "same app in two library folders shows once");
    }
}
