using System;
using System.IO;
using System.Linq;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>
/// الانحدار الذي ظهر في الاختبار الحي: عودة التركيز لنافذة Halawa كانت تنهي جلسة
/// لعبة Steam بعد 3 ثوانٍ فيُسجَّل 0. الآن تنتهي الجلسة من سجل Steam فقط.
/// </summary>
public class SteamPlaySessionTests : IDisposable
{
    private readonly string _dir;
    private readonly string _log;

    public SteamPlaySessionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "halawahub-steamsession-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _log = Path.Combine(_dir, "gameprocess_log.txt");
        File.WriteAllText(_log, "[2026-11-10 10:37:35] Client version: 1792618285\r\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private static GameInfo SteamGame(string appId) => new()
    {
        Id = appId, Name = "Spaceship Demo " + appId, Platform = "Steam",
        InstallPath = @"D:\GAME\Steam\steamapps\common\SpaceshipDemo",
        ExecutablePath = $"steam://rungameid/{appId}", IsInstalled = true
    };

    private void Append(string text) => File.AppendAllText(_log, text);

    [AvaloniaFact]
    public void WindowFocus_DoesNotEndASteamSession_AndTheLogDurationIsCredited()
    {
        const string appId = "9100001";
        using var ui = new UiHarness(games: new[] { SteamGame(appId) });
        var card = ui.Vm.Games.Single(g => g.Game.Id == appId);
        var tracker = new SteamSessionTracker(_log, appId, isProcessAlive: _ => true);

        ui.Vm.BeginPlaySession(card, null, tracker);
        Append($"[2026-11-10 11:18:40] AppID {appId} adding PID 13400 as a tracked process \"Spaceship.exe\"\r\n");
        ui.Vm.PollSteamSession();

        // نافذة "Starting game" في Steam تعيد التركيز لنافذتنا — كانت تنهي الجلسة هنا
        ui.Vm.OnWindowRegainedFocus();
        ui.Vm.PollSteamSession();

        Append($"[2026-11-10 11:21:31] AppID {appId} no longer tracking PID 13400, exit code 0\r\n");
        Append($"[2026-11-10 11:21:31] Remove {appId} from running list\r\n");
        ui.Vm.PollSteamSession();

        PlayTimeService.GetTotal("Steam", appId).Should().Be(TimeSpan.FromSeconds(171));
    }

    [AvaloniaFact]
    public void SteamGameThatNeverStarts_CreditsNoTime()
    {
        const string appId = "9100002";
        using var ui = new UiHarness(games: new[] { SteamGame(appId) });
        var card = ui.Vm.Games.Single(g => g.Game.Id == appId);
        var now = DateTime.Now;
        var tracker = new SteamSessionTracker(_log, appId, startupTimeout: TimeSpan.FromMinutes(3), now: () => now);

        ui.Vm.BeginPlaySession(card, null, tracker);
        now = now.AddMinutes(4);
        ui.Vm.PollSteamSession();

        // بعد إلغاء الجلسة لا يجوز أن يحتسب التركيز شيئًا ولا أن يعيد إحياءها
        ui.Vm.OnWindowRegainedFocus();
        Append($"[2026-11-10 11:30:00] AppID {appId} adding PID 1 as a tracked process \"x.exe\"\r\n");
        Append($"[2026-11-10 11:40:00] Remove {appId} from running list\r\n");
        ui.Vm.PollSteamSession();

        PlayTimeService.GetTotal("Steam", appId).Should().Be(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void StartingAnotherGame_CreditsTheRunningSteamSessionSoFar()
    {
        const string first = "9100003";
        const string second = "9100004";
        using var ui = new UiHarness(games: new[] { SteamGame(first), SteamGame(second) });
        var firstCard = ui.Vm.Games.Single(g => g.Game.Id == first);
        var secondCard = ui.Vm.Games.Single(g => g.Game.Id == second);

        var tracker = new SteamSessionTracker(_log, first, isProcessAlive: _ => true);
        ui.Vm.BeginPlaySession(firstCard, null, tracker);
        var startedAt = DateTime.Now.AddMinutes(-10);
        Append($"[{startedAt:yyyy-MM-dd HH:mm:ss}] AppID {first} adding PID 1 as a tracked process \"a.exe\"\r\n");
        ui.Vm.PollSteamSession();

        ui.Vm.BeginPlaySession(secondCard, null, new SteamSessionTracker(_log, second));

        PlayTimeService.GetTotal("Steam", first).TotalMinutes.Should().BeApproximately(10, 0.2);
    }
}
