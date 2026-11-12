using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using Xunit;

namespace HalawaHub.Tests;

/// القيم منسوخة من appmanifest_*.acf حقيقية على جهاز المالك (2026-11-10)
public class SteamStateFlagsTests
{
    private static GameStatus Status(long flags, string build = "100", string target = "100") =>
        GameStatusProbe.FromSteamState(new SteamManifestState(flags, build, target));

    [Fact]
    public void FullyInstalled_IsReady() => Status(4).Should().Be(GameStatus.None);

    [Fact]
    public void UpdateRequired_WithFilesMissing_IsAnUpdate_NotARepair()
    {
        // 1778820: StateFlags 38، و buildid = TargetBuildID، وملفات بانتظار التجهيز
        Status(38).Should().Be(GameStatus.UpdateAvailable);
    }

    [Fact]
    public void NewerTargetBuild_IsAnUpdate()
    {
        // 2358720 (Black Myth: Wukong): buildid 18095325 → TargetBuildID 20315824
        Status(38, "18095325", "20315824").Should().Be(GameStatus.UpdateAvailable);
        Status(4, "18095325", "20315824").Should().Be(GameStatus.UpdateAvailable);
        Status(4, "18095325", "0").Should().Be(GameStatus.None, "TargetBuildID 0 means no target");
    }

    [Fact]
    public void LockedWithUpdate_IsAnUpdate() => Status(22).Should().Be(GameStatus.UpdateAvailable);

    [Fact]
    public void UpdateStarted_IsStillAnUpdate() => Status(1026).Should().Be(GameStatus.UpdateAvailable);

    [Fact]
    public void Downloading_IsUpdating() =>
        Status(1048576 | 1024 | 4 | 2).Should().Be(GameStatus.Updating);

    [Fact]
    public void CorruptOrMissingFiles_NeedRepair()
    {
        Status(128 | 4).Should().Be(GameStatus.NeedsRepair);
        Status(32 | 4).Should().Be(GameStatus.NeedsRepair);
    }

    [Fact]
    public void AppRunningBit_IsRunning() => Status(64 | 4).Should().Be(GameStatus.Running);

    [Fact]
    public void Acf_IsParsed()
    {
        const string acf = "\"AppState\"\n{\n\t\"appid\"\t\t\"2358720\"\n\t\"StateFlags\"\t\t\"38\"\n\t\"buildid\"\t\t\"18095325\"\n" +
                           "\t\"TargetBuildID\"\t\t\"20315824\"\n}\n";
        var s = GameStatusProbe.ParseAcf(acf)!;
        s.StateFlags.Should().Be(38);
        s.BuildId.Should().Be("18095325");
        s.TargetBuildId.Should().Be("20315824");
        GameStatusProbe.ParseAcf("garbage").Should().BeNull();
    }

    [Fact]
    public void ManifestPath_IsNextToCommon()
    {
        var game = new GameInfo { Id = "2358720", Platform = "Steam", InstallPath = Path.Combine("D:", "GAME", "Steam", "steamapps", "common", "BlackMythWukong") };
        GameStatusProbe.SteamManifestPath(game).Should().Be(Path.Combine("D:", "GAME", "Steam", "steamapps", "appmanifest_2358720.acf"));
    }

    [Fact]
    public void LinuxRegistryVdf_GivesRunningAndUpdating()
    {
        const string vdf = "\"Apps\"\n{\n\"570\"\n{\n\"installed\"\t\"1\"\n\"Running\"\t\"1\"\n\"Updating\"\t\"0\"\n}\n\"440\"\n{\n\"Running\"\t\"0\"\n\"Updating\"\t\"1\"\n}\n}";
        GameStatusProbe.ParseRegistryVdfApp(vdf, "570").Should().Be((true, false));
        GameStatusProbe.ParseRegistryVdfApp(vdf, "440").Should().Be((false, true));
        GameStatusProbe.ParseRegistryVdfApp(vdf, "1").Should().BeNull();
    }
}

public class GameStatusProbeTests
{
    private static GameInfo Steam(string id, string install) => new() { Id = id, Name = id, Platform = "Steam", InstallPath = install };

    [Fact]
    public void GameOnADisconnectedDrive_IsOnlyThat()
    {
        var probe = new GameStatusProbe { Windows = true, DirExists = d => !d.StartsWith("X:", StringComparison.OrdinalIgnoreCase) };
        probe.Probe(Steam("1", @"X:\SteamLibrary\steamapps\common\Deadlock")).Should().Be(GameStatus.DriveNotConnected);
        probe.Probe(Steam("1", @"D:\SteamLibrary\steamapps\common\Deadlock")).Should().NotBe(GameStatus.DriveNotConnected);
    }

    [Fact]
    public void SteamProbe_CombinesManifestAndRegistry()
    {
        var install = Path.Combine("lib", "steamapps", "common", "Game");
        var files = new Dictionary<string, string>
        {
            [Path.Combine("lib", "steamapps", "appmanifest_570.acf")] = "\"StateFlags\" \"6\" \"buildid\" \"1\" \"TargetBuildID\" \"1\""
        };
        var probe = new GameStatusProbe
        {
            ReadText = p => files.TryGetValue(p, out var t) ? t : null,
            SteamAppRegistry = _ => (true, false),
            DirExists = _ => true
        };
        probe.Probe(Steam("570", install)).Should().Be(GameStatus.UpdateAvailable | GameStatus.Running);

        var updating = new GameStatusProbe { ReadText = probe.ReadText, SteamAppRegistry = _ => (false, true), DirExists = _ => true };
        updating.Probe(Steam("570", install)).Should().Be(GameStatus.Updating);
    }

    [Fact]
    public void EpicManifest_NeedsValidation_IsARepair()
    {
        const string json = "{\"AppName\":\"Fortnite\",\"bNeedsValidation\":true,\"bIsIncompleteInstall\":false}";
        GameStatusProbe.EpicStatusFromManifest(json, "Fortnite").Should().Be(GameStatus.NeedsRepair);
        GameStatusProbe.EpicStatusFromManifest(json.Replace("true", "false"), "Fortnite").Should().Be(GameStatus.None);
        GameStatusProbe.EpicStatusFromManifest(json, "Other").Should().BeNull();
    }
}

public class LauncherActionsTests
{
    private static GameInfo Game(string platform, string id = "1", string install = "", string exe = "") =>
        new() { Id = id, Name = "x", Platform = platform, InstallPath = install, ExecutablePath = exe };

    [Fact]
    public void Steam_UsesDocumentedUrls()
    {
        var a = new LauncherActions();
        a.OpenInLauncher(Game("Steam", "570"))!.Target.Should().Be("steam://nav/games/details/570");
        a.Verify(Game("Steam", "570"))!.Target.Should().Be("steam://validate/570");
        a.Uninstall(Game("Steam", "570"))!.Value.Command.Target.Should().Be("steam://uninstall/570");
        a.Verify(Game("Epic Games")).Should().BeNull("Epic has no documented verify link");
    }

    [Fact]
    public void Epic_OpensItsLauncher_ForUninstallToo()
    {
        var a = new LauncherActions { Windows = true, EpicLauncherExe = () => @"C:\Epic\EpicGamesLauncher.exe", FileExists = _ => true };
        a.OpenInLauncher(Game("Epic Games"))!.Target.Should().Be(@"C:\Epic\EpicGamesLauncher.exe");
        a.Uninstall(Game("Epic Games"))!.Value.OpensLauncher.Should().BeTrue();

        var none = new LauncherActions { Windows = true, EpicLauncherExe = () => null };
        none.OpenInLauncher(Game("Epic Games")).Should().BeNull();
        none.Uninstall(Game("Epic Games")).Should().BeNull();
    }

    [Fact]
    public void Gog_UninstallsWithItsOwnUninstaller_OnlyIfItExists()
    {
        var install = Path.Combine("C:", "GOG Games", "Witcher");
        var with = new LauncherActions { FileExists = p => p.EndsWith("unins000.exe") };
        with.Uninstall(Game("GOG", install: install))!.Value.Command.Target.Should().Be(Path.Combine(install, "unins000.exe"));
        new LauncherActions { FileExists = _ => false }.Uninstall(Game("GOG", install: install)).Should().BeNull();
    }

    [Fact]
    public void Xbox_OpensStoreUpdates_AndLinuxLaunchersHaveNoActionYet()
    {
        new LauncherActions { Windows = true }.OpenInLauncher(Game("Xbox / Microsoft Store"))!.Target
            .Should().Be("ms-windows-store://downloadsandupdates");
        new LauncherActions().OpenInLauncher(Game("Lutris")).Should().BeNull();
    }

    [Theory]
    [InlineData("\"C:\\Program Files (x86)\\Epic Games\\Launcher\\Portal\\Binaries\\Win64\\EpicGamesLauncher.exe\" %1",
        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe")]
    [InlineData(@"C:\x\a.exe %1", @"C:\x\a.exe")]
    [InlineData("", null)]
    public void ProtocolCommand_GivesTheExe(string command, string? exe)
    {
        LauncherActions.ExeFromCommand(command).Should().Be(exe);
    }
}

public class LibraryCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-libcache-" + Guid.NewGuid().ToString("N"));

    public LibraryCacheTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private static GameInfo G(string id, string install) => new() { Id = id, Name = id, Platform = "Steam", InstallPath = install, ExecutablePath = "steam://rungameid/" + id };

    [Fact]
    public void GamesOnAMissingDrive_StayInTheLibrary_UninstalledOnesDisappear()
    {
        var path = Path.Combine(_dir, "library-cache.json");
        bool xConnected = true;
        bool Missing(string? p) => p != null && p.StartsWith("X:") && !xConnected;

        LibraryCacheService.MergeOffline(path, new[] { G("1", @"X:\lib\a"), G("2", @"D:\lib\b"), G("3", @"D:\lib\c") }, Missing)
            .Should().HaveCount(3);

        // X: فُصل، و3 أُزيلت فعلًا من قرص موصول
        xConnected = false;
        var merged = LibraryCacheService.MergeOffline(path, new[] { G("2", @"D:\lib\b") }, Missing);
        merged.Select(g => g.Id).Should().BeEquivalentTo(new[] { "1", "2" });

        // فحص لاحق والقرص ما زال مفصولًا: تبقى
        LibraryCacheService.MergeOffline(path, new[] { G("2", @"D:\lib\b") }, Missing).Should().HaveCount(2);

        // وُصل القرص ولم يجدها الفحص: أُزيلت
        xConnected = true;
        LibraryCacheService.MergeOffline(path, new[] { G("2", @"D:\lib\b") }, Missing).Select(g => g.Id).Should().Equal("2");
    }
}
