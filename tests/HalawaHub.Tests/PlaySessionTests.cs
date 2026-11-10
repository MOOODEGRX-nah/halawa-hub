using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using HalawaHub.Core;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Sessions;
using Xunit;

namespace HalawaHub.Tests;

/// عمليات مزيفة: الاختبار يقرر ما "يعمل" الآن
internal sealed class FakeScanner : IProcessScanner
{
    public List<ProcessEntry> Running { get; } = new();
    public Dictionary<int, DateTime> StartTimes { get; } = new();

    public IReadOnlyList<ProcessEntry> Snapshot() => Running.ToList();
    public bool IsAlive(int pid) => Running.Any(p => p.Pid == pid);
    public DateTime? StartTimeUtc(int pid) => StartTimes.TryGetValue(pid, out var t) ? t : null;

    public void Start(int pid, string exe, params string[] args) => Running.Add(new ProcessEntry(pid, exe, args));
    public void Stop(int pid) => Running.RemoveAll(p => p.Pid == pid);
}

public class FolderSessionTrackerTests
{
    private const string Dir = @"D:\Epic\AlanWake2";
    private readonly FakeScanner _scanner = new();
    private DateTime _now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private FolderSessionTracker New(string dir = Dir) =>
        FolderSessionTracker.TryCreate(dir, _scanner, () => _now, windows: true, dirExists: _ => true)!;

    [Fact]
    public void GameStartsAndExits_TimeIsFromFirstToLastSighting()
    {
        _scanner.Start(1, @"C:\Windows\explorer.exe");
        var t = New();
        t.Poll().Should().Be(PlaySessionState.WaitingForStart);

        _now = _now.AddSeconds(20);
        _scanner.Start(50, @"D:\Epic\AlanWake2\AlanWake2.exe");
        t.Poll().Should().Be(PlaySessionState.Running);

        _now = _now.AddMinutes(30);
        t.Poll().Should().Be(PlaySessionState.Running);

        _scanner.Stop(50);
        _now = _now.AddSeconds(3);
        t.Poll().Should().Be(PlaySessionState.Running, "a short gap is allowed (launcher restarting the game)");
        _now = _now.AddSeconds(9);
        t.Poll().Should().Be(PlaySessionState.Ended);

        t.Elapsed().Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void GameAlreadyRunningBeforeLaunch_CountsNothing()
    {
        _scanner.Start(50, @"d:\epic\alanwake2\AlanWake2.exe");
        var t = New();

        t.State.Should().Be(PlaySessionState.NeverStarted);
        t.Failure.Should().Be(SessionFailure.AlreadyRunning);
        t.Elapsed().Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GameThatNeverAppears_TimesOutAfter180Seconds()
    {
        var t = New();
        _now = _now.AddSeconds(179);
        t.Poll().Should().Be(PlaySessionState.WaitingForStart);
        _now = _now.AddSeconds(2);
        t.Poll().Should().Be(PlaySessionState.NeverStarted);
        t.Failure.Should().Be(SessionFailure.StartTimeout);
    }

    [Fact]
    public void CrashHandlerLeftBehind_DoesNotKeepTheSessionAlive()
    {
        var t = New();
        _scanner.Start(50, @"D:\Epic\AlanWake2\AlanWake2.exe");
        _scanner.Start(51, @"D:\Epic\AlanWake2\UnityCrashHandler64.exe");
        t.Poll().Should().Be(PlaySessionState.Running);

        _scanner.Stop(50);
        _now = _now.AddSeconds(20);
        t.Poll().Should().Be(PlaySessionState.Ended);
    }

    [Fact]
    public void SimilarFolderName_IsNotTheGame()
    {
        var t = New();
        _scanner.Start(50, @"D:\Epic\AlanWake2 Remastered\game.exe");
        t.Poll().Should().Be(PlaySessionState.WaitingForStart);
    }

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"D:\Games")]
    [InlineData("")]
    public void TooBroadOrEmptyFolders_AreNotTracked(string dir)
    {
        // "D:\Games" has 2 parts so it is allowed; only roots / empty are refused here
        var t = FolderSessionTracker.TryCreate(dir, _scanner, () => _now, windows: true, dirExists: _ => true);
        if (dir == @"D:\Games") t.Should().NotBeNull();
        else t.Should().BeNull();
    }

    [Fact]
    public void ProgramFilesItself_IsTooBroad()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(pf)) return;
        GameFolderMatcher.IsTooBroad(pf, OperatingSystem.IsWindows()).Should().BeTrue();
    }

    [Fact]
    public void MissingFolder_IsNotTracked()
    {
        FolderSessionTracker.TryCreate(Dir, _scanner, () => _now, windows: true, dirExists: _ => false).Should().BeNull();
    }

    [Fact]
    public void LinuxWineGame_IsMatchedFromItsCommandLine()
    {
        const string dir = "/home/me/Games/Heroic/Hades";
        var t = FolderSessionTracker.TryCreate(dir, _scanner, () => _now, windows: false, dirExists: _ => true)!;

        _scanner.Start(70, "/usr/bin/wine64-preloader", "Z:\\home\\me\\Games\\Heroic\\Hades\\x64\\Hades.exe");
        t.Poll().Should().Be(PlaySessionState.Running);
    }

    [Fact]
    public void LinuxPaths_AreCaseSensitive()
    {
        GameFolderMatcher.PathIsInside("/home/me/games/hades/x", "/home/me/Games/hades", windows: false).Should().BeFalse();
        GameFolderMatcher.PathIsInside(@"d:\GAMES\x\y.exe", @"D:\games\x", windows: true).Should().BeTrue();
        GameFolderMatcher.PathIsInside(@"\\?\D:\games\x\y.exe", @"D:\games\x", windows: true).Should().BeTrue();
    }

    [Fact]
    public void Resume_KeepsTheOriginalStartTime()
    {
        var started = _now.AddHours(-1);
        _scanner.Start(50, @"D:\Epic\AlanWake2\AlanWake2.exe");
        var t = FolderSessionTracker.Resume(Dir, started, _now, _scanner, () => _now, windows: true);

        t.Poll().Should().Be(PlaySessionState.Running);
        t.Elapsed().Should().Be(TimeSpan.FromHours(1));
    }
}

public class ProcessSessionTrackerTests
{
    [Fact]
    public void EndsWhenTheProcessExits()
    {
        var scanner = new FakeScanner();
        var now = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        scanner.Start(9, @"D:\x\game.exe");
        var t = new ProcessSessionTracker(9, scanner, () => now);

        now = now.AddMinutes(5);
        t.Poll().Should().Be(PlaySessionState.Running);
        scanner.Stop(9);
        now = now.AddSeconds(3);
        t.Poll().Should().Be(PlaySessionState.Ended);
        t.Elapsed().Should().Be(TimeSpan.FromSeconds(303));
    }
}

public class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-atomic-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private static Dictionary<string, long>? Parse(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, long>>(json);

    [Fact]
    public void SecondWrite_KeepsTheFirstAsBak_AndLeavesNoTempFile()
    {
        var path = Path.Combine(_dir, "playtime.json");
        AtomicFile.WriteAllText(path, "{\"a\":1}");
        AtomicFile.WriteAllText(path, "{\"a\":2}");

        File.ReadAllText(path).Should().Be("{\"a\":2}");
        File.ReadAllText(path + ".bak").Should().Be("{\"a\":1}");
        Directory.GetFiles(_dir).Should().HaveCount(2);
    }

    [Fact]
    public void CorruptFile_IsReadFromBak()
    {
        var path = Path.Combine(_dir, "playtime.json");
        AtomicFile.WriteAllText(path, "{\"Steam|1\":100}");
        AtomicFile.WriteAllText(path, "{\"Steam|1\":200}");
        File.WriteAllText(path, "{\"Steam|1\":2"); // نصف مكتوب

        var data = AtomicFile.ReadWithBackup(path, Parse, "test");
        data.Should().NotBeNull();
        data!["Steam|1"].Should().Be(100);
    }

    [Fact]
    public void MissingFile_ReturnsNull()
    {
        AtomicFile.ReadWithBackup(Path.Combine(_dir, "none.json"), Parse, "test").Should().BeNull();
    }

    [Fact]
    public void OldPlainFile_StillLoads()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "playtime.json");
        File.WriteAllText(path, "{\"Steam|570\":3600}");
        AtomicFile.ReadWithBackup(path, Parse, "test")!["Steam|570"].Should().Be(3600);
    }
}

public class ActiveSessionRecoveryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-recover-" + Guid.NewGuid().ToString("N"));
    private readonly FakeScanner _scanner = new();
    private readonly DateTime _now = DateTime.UtcNow;

    public ActiveSessionRecoveryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private ActiveSessionRecord Record(string kind, string dir) => new()
    {
        Platform = "Epic Games", Id = "alan", Name = "Alan Wake 2", Kind = kind, InstallDir = dir,
        StartedUtc = _now.AddMinutes(-50), LastSeenUtc = _now.AddMinutes(-10)
    };

    [Fact]
    public void FolderGameStillRunning_IsResumedFromItsOriginalStart()
    {
        var dir = Path.Combine(_dir, "AlanWake2");
        _scanner.Start(5, Path.Combine(dir, "AlanWake2.exe"));

        var r = ActiveSessionRecovery.Recover(Record("folder", dir), _scanner, () => _now);
        r.Resumed.Should().NotBeNull();
        r.Resumed!.StartedAtUtc.Should().Be(_now.AddMinutes(-50));
    }

    [Fact]
    public void FolderGameNoLongerRunning_IsClosedAtLastSeen()
    {
        var r = ActiveSessionRecovery.Recover(Record("folder", Path.Combine(_dir, "AlanWake2")), _scanner, () => _now);
        r.Resumed.Should().BeNull();
        r.ClosedDuration.Should().Be(TimeSpan.FromMinutes(40));
    }

    [Fact]
    public void ReusedPidAfterReboot_IsNotOurGame()
    {
        var rec = Record("process", "");
        rec.Pids = new List<int> { 77 };
        _scanner.Start(77, @"C:\Windows\notepad.exe");
        _scanner.StartTimes[77] = _now.AddMinutes(-1); // بدأت بعد بداية الجلسة بكثير

        var r = ActiveSessionRecovery.Recover(rec, _scanner, () => _now);
        r.Resumed.Should().BeNull();
        r.ClosedDuration.Should().Be(TimeSpan.FromMinutes(40));
    }

    [Fact]
    public void SteamEndLineWrittenWhileHalawaWasClosed_GivesTheExactDuration()
    {
        var log = Path.Combine(_dir, "logs", "gameprocess_log.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        File.WriteAllText(log, "[2026-11-10 10:00:00] Client version: 1\r\n");
        var offset = new FileInfo(log).Length;

        var startedLocal = new DateTime(2026, 11, 10, 11, 0, 0, DateTimeKind.Local);
        File.AppendAllText(log, "[2026-11-10 11:42:00] AppID 570 no longer tracking PID 1, exit code 0\r\n" +
                                "[2026-11-10 11:42:00] Remove 570 from running list\r\n");

        var rec = new ActiveSessionRecord
        {
            Platform = "Steam", Id = "570", Name = "Dota 2", Kind = "steam", Pids = new List<int> { 1 },
            StartedUtc = startedLocal.ToUniversalTime(), LastSeenUtc = startedLocal.AddMinutes(5).ToUniversalTime(),
            SteamLogPath = log, SteamLogOffset = offset
        };

        var r = ActiveSessionRecovery.Recover(rec, _scanner, () => startedLocal.AddHours(1).ToUniversalTime());
        r.Resumed.Should().BeNull();
        r.ClosedDuration.Should().Be(TimeSpan.FromMinutes(42));
    }

    [Fact]
    public void VeryOldRecord_IsClosedWithoutResuming()
    {
        var dir = Path.Combine(_dir, "AlanWake2");
        _scanner.Start(5, Path.Combine(dir, "AlanWake2.exe"));
        var rec = Record("folder", dir);

        var r = ActiveSessionRecovery.Recover(rec, _scanner, () => _now.AddDays(3));
        r.Resumed.Should().BeNull();
    }

    [Fact]
    public void Store_RoundTripsAndDeletesTheBackupToo()
    {
        var path = Path.Combine(_dir, "active-session.json");
        ActiveSessionStore.Save(path, Record("folder", @"D:\x\y"));
        ActiveSessionStore.Save(path, Record("folder", @"D:\x\y"));
        ActiveSessionStore.Load(path)!.Name.Should().Be("Alan Wake 2");

        ActiveSessionStore.Delete(path);
        File.Exists(path).Should().BeFalse();
        File.Exists(path + ".bak").Should().BeFalse();
        ActiveSessionStore.Load(path).Should().BeNull();
    }
}

public class SteamFlatpakTrackingTests
{
    [Theory]
    [InlineData("/home/me/.var/app/com.valvesoftware.Steam/.local/share/Steam", true)]
    [InlineData("/home/me/.local/share/Steam", false)]
    [InlineData(@"c:/program files (x86)/steam", false)]
    public void FlatpakSteam_IsDetectedFromItsPath(string path, bool expected)
    {
        SteamSessionTracker.IsFlatpakSteam(path).Should().Be(expected);
    }

    [Fact]
    public void EmptyLogRead_DoesNotThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halawahub-steamempty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var log = Path.Combine(dir, "gameprocess_log.txt");
            File.WriteAllText(log, "");
            var t = new SteamSessionTracker(log, "1", isProcessAlive: _ => true);
            t.Poll().Should().Be(SteamSessionState.WaitingForStart);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

public class LaunchPreflightTests
{
    private static GameInfo Game(string platform, string install, string exe) => new()
    {
        Id = "1", Name = "X", Platform = platform, InstallPath = install, ExecutablePath = exe
    };

    private static LaunchPreflight Windows(Func<string, bool> dirs, Func<string, bool>? files = null,
        Func<string, bool>? protocols = null, bool? signedIn = true) => new()
    {
        DirExists = dirs,
        FileExists = files ?? (_ => true),
        ProtocolRegistered = protocols ?? (_ => true),
        SteamSignedIn = () => signedIn,
        Windows = true
    };

    [Fact]
    public void DisconnectedDrive_BlocksWithTheDriveName()
    {
        var p = Windows(d => !d.StartsWith("X:", StringComparison.OrdinalIgnoreCase));
        var (problem, detail) = p.Check(Game("Steam", @"X:\SteamLibrary\steamapps\common\Hades", "steam://rungameid/1"));
        problem.Should().Be(LaunchProblem.DriveNotConnected);
        detail.Should().Be("X:");
        LaunchPreflight.BlocksLaunch(problem).Should().BeTrue();
    }

    [Fact]
    public void DeletedFolderOnAConnectedDrive_IsMissingFiles()
    {
        var p = Windows(d => d == @"D:\");
        p.Check(Game("GOG", @"D:\GOG\Witcher", @"D:\GOG\Witcher\witcher.exe")).Problem
            .Should().Be(LaunchProblem.GameFilesMissing);
    }

    [Fact]
    public void UnregisteredLauncherProtocol_IsLauncherNotFound()
    {
        var p = Windows(_ => true, protocols: s => s != "com.epicgames.launcher");
        var (problem, detail) = p.Check(Game("Epic Games", @"D:\Epic\A", "com.epicgames.launcher://apps/a?action=launch"));
        problem.Should().Be(LaunchProblem.LauncherNotFound);
        detail.Should().Be("Epic Games Launcher");
    }

    [Fact]
    public void MissingRiotClient_IsLauncherNotFound()
    {
        var p = Windows(_ => true, files: _ => false);
        p.Check(Game("Riot Games", @"C:\Riot Games\VALORANT\live", @"C:\Riot Games\Riot Client\RiotClientServices.exe"))
            .Problem.Should().Be(LaunchProblem.LauncherNotFound);
    }

    [Fact]
    public void SteamSignedOut_WarnsButDoesNotBlock()
    {
        var p = Windows(_ => true, signedIn: false);
        var problem = p.Check(Game("Steam", @"D:\S\common\H", "steam://rungameid/1")).Problem;
        problem.Should().Be(LaunchProblem.SteamNotSignedIn);
        LaunchPreflight.BlocksLaunch(problem).Should().BeFalse();
    }

    [Fact]
    public void HealthyGame_HasNoProblem()
    {
        Windows(_ => true, signedIn: null).Check(Game("Steam", @"D:\S\common\H", "steam://rungameid/1")).Problem
            .Should().Be(LaunchProblem.None);
        // Xbox: explorer.exe بلا مسار لا يُفحص كملف
        Windows(_ => true, files: _ => false).Check(Game("Xbox / Microsoft Store", @"C:\XboxGames\F", "explorer.exe")).Problem
            .Should().Be(LaunchProblem.None);
    }

    [Fact]
    public void LinuxUnmountedDisk_IsDriveNotConnected()
    {
        var p = new LaunchPreflight { DirExists = _ => false, Windows = false, SteamSignedIn = () => null };
        var (problem, detail) = p.Check(Game("Lutris", "/run/media/me/Games/lutris/x", "lutris:rungameid/3"));
        problem.Should().Be(LaunchProblem.DriveNotConnected);
        detail.Should().Be("/run/media/me/Games");
    }

    [Theory]
    [InlineData("steam://rungameid/1", "steam")]
    [InlineData("lutris:rungameid/3", "lutris")]
    [InlineData("heroic://launch/legendary/x", "heroic")]
    [InlineData(@"C:\Games\x.exe", null)]
    [InlineData("/usr/bin/game", null)]
    [InlineData("explorer.exe", null)]
    public void UrlScheme_IsReadOnlyFromRealUrls(string exe, string? scheme)
    {
        LaunchPreflight.UrlScheme(exe).Should().Be(scheme);
    }

    [Fact]
    public void LinuxRegistryVdf_GivesPidAndActiveUser()
    {
        const string vdf = "\"Registry\"\n{\n\t\"HKCU\"\n\t{\n\t\t\"Software\"\n\t\t{\n\t\t\t\"Valve\"\n\t\t\t{\n\t\t\t\t\"Steam\"\n\t\t\t\t{\n" +
                           "\t\t\t\t\t\"ActiveProcess\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"pid\"\t\t\"4242\"\n\t\t\t\t\t\t\"ActiveUser\"\t\t\"0\"\n\t\t\t\t\t}\n" +
                           "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";
        var (pid, user) = LaunchPreflight.ParseRegistryVdf(vdf);
        pid.Should().Be(4242);
        user.Should().Be(0);
        LaunchPreflight.SignedInFrom(pid!.Value, user!.Value).Should().BeFalse();
        LaunchPreflight.SignedInFrom(0, 0).Should().BeNull("Steam isn't running, so we don't know");
    }
}
