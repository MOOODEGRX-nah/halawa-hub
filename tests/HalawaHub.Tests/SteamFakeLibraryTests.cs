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

    /// <summary>
    /// انحدار من جهاز حقيقي: Steam كتب /mnt/nvme-game/game/Steam والمجلد على ext4 هو
    /// /mnt/nvme-game/GAME/STEAM، فاختفت كل ألعاب تلك المكتبة من البرنامج بصمت.
    /// </summary>
    [Fact]
    public void LibraryPath_WithDifferentCase_IsStillFound_OnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        var realLibrary = Path.Combine(_root, "GAME", "STEAM");
        Directory.CreateDirectory(Path.Combine(realLibrary, "steamapps", "common", "The Finals"));
        File.WriteAllText(Path.Combine(realLibrary, "steamapps", "appmanifest_2073850.acf"),
            "\"AppState\"\n{\n\t\"appid\"\t\t\"2073850\"\n\t\"name\"\t\t\"THE FINALS\"\n\t\"installdir\"\t\t\"The Finals\"\n}\n");

        File.WriteAllText(Path.Combine(_root, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{_root}/game/Steam\"\n\t}}\n}}\n");

        var game = SteamLibraryProvider.ScanFromSteamPath(_root).Should().ContainSingle().Subject;

        game.Name.Should().Be("THE FINALS");
        game.InstallPath.Should().Be(Path.Combine(realLibrary, "steamapps", "common", "The Finals"));
        game.IsInstalled.Should().BeTrue();
    }

    [Fact]
    public void InstallDir_WithDifferentCase_IsStillInstalled_OnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        Directory.CreateDirectory(Path.Combine(_root, "steamapps", "common", "The Finals"));
        WriteManifest("2073850", "THE FINALS", "the finals", createDir: false);

        var game = SteamLibraryProvider.ScanFromSteamPath(_root).Should().ContainSingle().Subject;

        game.IsInstalled.Should().BeTrue();
        game.InstallPath.Should().EndWith("The Finals");
    }

    [Fact]
    public void ResolveExistingDirectory_PrefersTheExactPath_AndLeavesMissingPathsAlone()
    {
        if (!OperatingSystem.IsLinux()) return;

        var lower = Path.Combine(_root, "lib");
        var upper = Path.Combine(_root, "LIB");
        Directory.CreateDirectory(lower);
        Directory.CreateDirectory(upper);

        // مجلدان يختلفان بحالة الأحرف ممكنان على لينكس، والمطابق تمامًا هو الصحيح
        SteamLibraryProvider.ResolveExistingDirectory(lower).Should().Be(lower);
        SteamLibraryProvider.ResolveExistingDirectory(upper).Should().Be(upper);

        var missing = Path.Combine(_root, "nowhere", "at", "all");
        SteamLibraryProvider.ResolveExistingDirectory(missing).Should().Be(missing);
    }

    [Fact]
    public void TwoLibraries_DifferingOnlyByCase_AreBothScanned_OnLinux()
    {
        if (!OperatingSystem.IsLinux()) return;

        foreach (var (folder, id) in new[] { ("lib", "1"), ("LIB", "2") })
        {
            var steamApps = Path.Combine(_root, folder, "steamapps");
            Directory.CreateDirectory(Path.Combine(steamApps, "common", "g" + id));
            File.WriteAllText(Path.Combine(steamApps, $"appmanifest_{id}.acf"),
                $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{id}\"\n\t\"name\"\t\t\"Game {id}\"\n\t\"installdir\"\t\t\"g{id}\"\n}}\n");
        }

        File.WriteAllText(Path.Combine(_root, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n" +
            $"\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{_root}/lib\"\n\t}}\n" +
            $"\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{_root}/LIB\"\n\t}}\n" +
            "}\n");

        SteamLibraryProvider.ScanFromSteamPath(_root).Select(g => g.Id).Should().BeEquivalentTo(new[] { "1", "2" });
    }
}
