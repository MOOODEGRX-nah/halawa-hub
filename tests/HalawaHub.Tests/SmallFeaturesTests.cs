using System;
using System.IO;
using FluentAssertions;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using Xunit;

namespace HalawaHub.Tests;

public class ManualGamesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-manual-" + Guid.NewGuid().ToString("N"));

    public ManualGamesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private string Touch(string name, string content = "x")
    {
        var p = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void Executable_RunsItself_AndIsTrackedByItsFolder()
    {
        var exe = Touch(Path.Combine("Emu", "dolphin.exe"));
        var r = ManualGames.FromFile(exe)!;
        r.Name.Should().Be("dolphin");
        r.Target.Should().Be(exe);
        r.InstallDir.Should().Be(Path.Combine(_dir, "Emu"));
        r.Id.Should().HaveLength(32);
    }

    [Fact]
    public void UrlFile_UsesItsUrl()
    {
        var url = Touch("Deadlock.url", "[InternetShortcut]\r\nIDList=\r\nURL=steam://rungameid/1422450\r\nIconIndex=0\r\n");
        var r = ManualGames.FromFile(url)!;
        r.Target.Should().Be("steam://rungameid/1422450");
        r.InstallDir.Should().BeEmpty();
        r.Name.Should().Be("Deadlock");
    }

    [Fact]
    public void Shortcut_RunsTheLnk_AndTracksItsTargetFolder()
    {
        var target = Touch(Path.Combine("Games", "X", "x.exe"));
        var lnk = Touch("X.lnk");
        var r = ManualGames.FromFile(lnk, _ => target)!;
        r.Target.Should().Be(lnk);
        r.InstallDir.Should().Be(Path.GetDirectoryName(target));
    }

    [Fact]
    public void DesktopFile_ReadsNameExecAndPath_WithoutFieldCodes()
    {
        const string desktop = "[Desktop Entry]\nType=Application\nName=Celeste\nExec=\"/opt/celeste/Celeste\" --fullscreen %U\nPath=/opt/celeste\n";
        var r = ManualGames.FromDesktopFile(desktop, new ManualGameRecord { Id = "1", Name = "file" })!;
        r.Name.Should().Be("Celeste");
        r.Target.Should().Be("/opt/celeste/Celeste");
        r.Arguments.Should().Be("--fullscreen");
        r.InstallDir.Should().Be("/opt/celeste");
    }

    [Fact]
    public void DesktopFileWithoutExec_IsRejected()
    {
        ManualGames.FromDesktopFile("[Desktop Entry]\nName=x\n", new ManualGameRecord()).Should().BeNull();
        ManualGames.FromFile(Path.Combine(_dir, "missing.exe")).Should().BeNull();
    }

    [Fact]
    public void Records_RoundTrip_AndBecomeGames()
    {
        var path = Path.Combine(_dir, "manual-games.json");
        ManualGames.Save(path, new() { new ManualGameRecord { Id = "a", Name = "A", Target = "C:\\a.exe", Arguments = "-x", InstallDir = "C:\\A" } });

        var games = new ManualGamesProvider(path).ScanLibrary();
        var g = System.Linq.Enumerable.Single(games);
        g.Platform.Should().Be(ManualGames.Platform);
        g.ExecutablePath.Should().Be("C:\\a.exe");
        g.LaunchArguments.Should().Be("-x");
    }
}

public class DesktopShortcutTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-desk-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void LinuxDesktopEntry_QuotesTheCommand()
    {
        var content = DesktopShortcut.DesktopFileContent("Hades", "steam://rungameid/1145360", null, isUrl: true);
        content.Should().Contain("Exec=xdg-open \"steam://rungameid/1145360\"");
        content.Should().Contain("Type=Application").And.Contain("Name=Hades");

        DesktopShortcut.DesktopFileContent("G", "/home/me/My $Game/run", "-w", isUrl: false)
            .Should().Contain("Exec=\"/home/me/My \\$Game/run\" -w");
    }

    [Fact]
    public void FileNames_AreSafe_AndXdgDesktopIsRead()
    {
        DesktopShortcut.SafeFileName("Halo: MCC / 2?").Should().Be("Halo  MCC   2");
        DesktopShortcut.SafeFileName("...").Should().Be("Game");
        DesktopShortcut.ParseXdgDesktopDir("XDG_DOWNLOAD_DIR=\"$HOME/Downloads\"\nXDG_DESKTOP_DIR=\"$HOME/سطح المكتب\"\n", "/home/me")
            .Should().Be("/home/me/سطح المكتب");
    }

    [Fact]
    public void UrlGame_GetsAUrlFileOnWindows_OrADesktopFileOnLinux_WithoutOverwriting()
    {
        var game = new GameInfo { Id = "570", Name = "Dota 2", Platform = "Steam", ExecutablePath = "steam://rungameid/570" };
        var first = DesktopShortcut.Create(game, "Dota 2", _dir);
        var second = DesktopShortcut.Create(game, "Dota 2", _dir);

        first.Should().NotBe(second, "an existing shortcut is never overwritten");
        if (OperatingSystem.IsWindows())
        {
            Path.GetExtension(first).Should().Be(".url");
            File.ReadAllText(first).Should().Contain("URL=steam://rungameid/570");
        }
        else
        {
            Path.GetExtension(first).Should().Be(".desktop");
            File.GetUnixFileMode(first).HasFlag(UnixFileMode.UserExecute).Should().BeTrue();
        }
    }

    [Fact]
    public void ExeGame_GetsALnkWithItsArguments_OnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
        var game = new GameInfo { Id = "x", Name = "Pad", Platform = "Riot Games", ExecutablePath = exe, LaunchArguments = "--launch-product=valorant" };

        var lnk = DesktopShortcut.Create(game, "Pad", _dir);
        Path.GetExtension(lnk).Should().Be(".lnk");
        new FileInfo(lnk).Length.Should().BeGreaterThan(0);
    }
}
