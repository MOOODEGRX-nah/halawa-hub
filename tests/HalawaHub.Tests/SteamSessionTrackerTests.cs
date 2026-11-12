using System;
using System.IO;
using FluentAssertions;
using HalawaHub.Core.Library;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// تتبع جلسات Steam من logs/gameprocess_log.txt. السطور منسوخة من سجل حقيقي:
/// جلسة Spaceship Demo مدتها الفعلية 152 ثانية، وكان Halawa يسجّل لها 0
/// لأن عودة التركيز لنافذته كانت تنهي الجلسة بعد 3 ثوانٍ.
/// </summary>
public class SteamSessionTrackerTests : IDisposable
{
    private const string AppId = "1605230";
    private readonly string _dir;
    private readonly string _log;
    private DateTime _now = new(2026, 11, 10, 11, 10, 40, DateTimeKind.Local);

    public SteamSessionTrackerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "halawahub-steamlog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _log = Path.Combine(_dir, "gameprocess_log.txt");
        File.WriteAllText(_log, "[2026-11-10 10:37:35] Client version: 1792618285\r\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private SteamSessionTracker NewTracker(Func<int, bool>? alive = null, TimeSpan? startup = null, TimeSpan? grace = null) =>
        new(_log, AppId, startup, grace, alive ?? (_ => true), () => _now);

    private void Append(string text) => File.AppendAllText(_log, text);

    private static string Added(string time, int pid, string app = AppId) =>
        $"[2026-11-10 {time}] AppID {app} adding PID {pid} as a tracked process \"D:\\GAME\\Steam\\steamapps\\common\\SpaceshipDemo\\Spaceship.exe\"\r\n";

    private static string Removed(string time, int pid, string app = AppId) =>
        $"[2026-11-10 {time}] AppID {app} no longer tracking PID {pid}, exit code 0\r\n";

    private static string Stopped(string time, string app = AppId) =>
        $"[2026-11-10 {time}] Remove {app} from running list\r\n";

    [Fact]
    public void ParseLine_ReadsTheThreeSteamEvents()
    {
        var added = SteamGameProcessLog.ParseLine(Added("11:10:45", 14724).TrimEnd());
        added.Should().NotBeNull();
        added!.Kind.Should().Be(SteamLogEventKind.ProcessAdded);
        added.AppId.Should().Be(AppId);
        added.Pid.Should().Be(14724);
        added.Timestamp.Should().Be(new DateTime(2026, 11, 10, 11, 10, 45));

        var removed = SteamGameProcessLog.ParseLine(Removed("11:13:17", 14724).TrimEnd());
        removed!.Kind.Should().Be(SteamLogEventKind.ProcessRemoved);
        removed.Pid.Should().Be(14724);

        var stopped = SteamGameProcessLog.ParseLine(Stopped("11:13:17").TrimEnd());
        stopped!.Kind.Should().Be(SteamLogEventKind.AppStopped);
        stopped.AppId.Should().Be(AppId);
        stopped.Pid.Should().BeNull();
    }

    [Theory]
    [InlineData("[2026-11-10 11:10:45] SSGL: InternalUpdateClientGame indicates change to games list")]
    [InlineData("[2026-11-10 11:10:46] SSGL: change [1605230] LCT 1576343872->0")]
    [InlineData("[2026-11-10 10:37:35] Client version: 1792618285")]
    [InlineData("")]
    public void ParseLine_IgnoresUnrelatedLines(string line)
    {
        SteamGameProcessLog.ParseLine(line).Should().BeNull();
    }

    [Fact]
    public void RealSession_IsMeasuredFromSteamTimestamps()
    {
        var tracker = NewTracker();

        Append(Added("11:10:45", 14724));
        Append($"[2026-11-10 11:10:47] AppID {AppId} adding PID 12056 as a tracked process \"\"D:\\GAME\\UnityCrashHandler64.exe\" --attach 14724\"\r\n");
        tracker.Poll().Should().Be(SteamSessionState.Running);

        Append(Removed("11:13:17", 12056));
        Append(Removed("11:13:17", 14724));
        Append(Stopped("11:13:17"));

        tracker.Poll().Should().Be(SteamSessionState.Ended);
        tracker.Elapsed(DateTime.Now).Should().Be(TimeSpan.FromSeconds(152));
    }

    [Fact]
    public void LinesWrittenBeforeLaunch_AreIgnored()
    {
        // جلسة قديمة كاملة لنفس اللعبة قبل الضغط على "تشغيل"
        Append(Added("09:00:00", 1));
        Append(Stopped("09:30:00"));

        var tracker = NewTracker();

        tracker.Poll().Should().Be(SteamSessionState.WaitingForStart);
        tracker.Elapsed(DateTime.Now).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void OtherGames_DoNotStartOrEndTheSession()
    {
        var tracker = NewTracker();

        Append(Added("11:00:00", 50, app: "730"));
        tracker.Poll().Should().Be(SteamSessionState.WaitingForStart);

        Append(Added("11:01:00", 60));
        Append(Stopped("11:02:00", app: "730"));
        tracker.Poll().Should().Be(SteamSessionState.Running);
    }

    [Fact]
    public void HalfWrittenLine_IsReadWhenComplete()
    {
        var tracker = NewTracker();
        var line = Added("11:10:45", 14724);

        Append(line[..30]);
        tracker.Poll().Should().Be(SteamSessionState.WaitingForStart);

        Append(line[30..]);
        tracker.Poll().Should().Be(SteamSessionState.Running);
        tracker.StartedAt.Should().Be(new DateTime(2026, 11, 10, 11, 10, 45));
    }

    [Fact]
    public void GameThatNeverStarts_CreditsNothing()
    {
        var tracker = NewTracker(startup: TimeSpan.FromMinutes(3));

        _now = _now.AddMinutes(2);
        tracker.Poll().Should().Be(SteamSessionState.WaitingForStart);

        _now = _now.AddMinutes(2);
        tracker.Poll().Should().Be(SteamSessionState.NeverStarted);
        tracker.Elapsed(_now).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void WhileRunning_ElapsedGrowsWithTheClock()
    {
        var tracker = NewTracker();
        Append(Added("11:10:45", 14724));
        tracker.Poll();

        tracker.Elapsed(new DateTime(2026, 11, 10, 11, 20, 45)).Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void SteamNeverWritesTheEnd_SessionEndsWhenProcessesAreGone()
    {
        var alive = true;
        var tracker = NewTracker(alive: _ => alive, grace: TimeSpan.FromSeconds(15));
        Append(Added("11:10:45", 14724));
        _now = new DateTime(2026, 11, 10, 11, 10, 46);
        tracker.Poll().Should().Be(SteamSessionState.Running);

        // Steam انهار: اللعبة أُغلقت ولا يوجد سطر "Remove … from running list"
        alive = false;
        _now = new DateTime(2026, 11, 10, 11, 20, 45);
        tracker.Poll().Should().Be(SteamSessionState.Running, "grace period not over yet");

        _now = _now.AddSeconds(16);
        tracker.Poll().Should().Be(SteamSessionState.Ended);
        tracker.Elapsed(_now).Should().Be(TimeSpan.FromMinutes(10), "the session ends when the processes were first seen dead");
    }

    [Fact]
    public void LauncherHandingOverToTheGame_KeepsTheSession()
    {
        var alivePids = new System.Collections.Generic.HashSet<int> { 1 };
        var tracker = NewTracker(alive: alivePids.Contains, grace: TimeSpan.FromSeconds(15));
        Append(Added("11:10:45", 1));
        tracker.Poll();

        // المشغّل الوسيط خرج، وبعد 5 ثوانٍ أضاف Steam عملية اللعبة الحقيقية
        alivePids.Clear();
        tracker.Poll();
        _now = _now.AddSeconds(5);
        alivePids.Add(2);
        Append(Added("11:10:50", 2));
        tracker.Poll().Should().Be(SteamSessionState.Running);

        _now = _now.AddMinutes(5);
        tracker.Poll().Should().Be(SteamSessionState.Running);
    }

    [Fact]
    public void SteamRestartStartsANewLog_TrackerFollowsIt()
    {
        File.WriteAllText(_log, new string('x', 4000) + "\r\n");
        var tracker = NewTracker();

        // Steam أعاد التشغيل فبدأ سجلًا أقصر من موضع قراءتنا
        File.WriteAllText(_log, Added("11:10:45", 14724));
        tracker.Poll().Should().Be(SteamSessionState.Running);
    }

    [Fact]
    public void MissingLog_IsNotAnError()
    {
        var tracker = NewTracker();
        File.Delete(_log);

        tracker.Poll().Should().Be(SteamSessionState.WaitingForStart);
    }
}
