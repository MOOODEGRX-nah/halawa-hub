using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Models;
using HalawaHub.Core.Sessions;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// جلسات المنصات غير Steam: تتبع بالمجلد بدل تركيز النافذة، واستعادة الجلسة المحفوظة
public class PlaySessionUiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-sessionui-" + Guid.NewGuid().ToString("N"));

    public PlaySessionUiTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private GameInfo EpicGame(string id, string? install = null) => new()
    {
        Id = id, Name = "Epic " + id, Platform = "Epic Games",
        InstallPath = install ?? Path.Combine(_dir, id),
        ExecutablePath = $"com.epicgames.launcher://apps/{id}?action=launch", IsInstalled = true
    };

    [AvaloniaFact]
    public void FolderTrackedSession_IgnoresWindowFocus_AndCreditsTheRealTime()
    {
        var id = "fold" + Guid.NewGuid().ToString("N")[..6];
        var game = EpicGame(id);
        Directory.CreateDirectory(game.InstallPath);
        using var ui = new UiHarness(games: new[] { game });
        ui.Vm.ActiveSessionPath = Path.Combine(_dir, "active-session.json");
        var card = ui.Vm.Games.Single(g => g.Game.Id == id);

        var scanner = new FakeScanner();
        var now = DateTime.UtcNow;
        var tracker = FolderSessionTracker.TryCreate(game.InstallPath, scanner, () => now)!;
        ui.Vm.BeginPlaySession(card, tracker);

        scanner.Start(10, Path.Combine(game.InstallPath, "Game.exe"));
        ui.Vm.PollSession();
        File.Exists(ui.Vm.ActiveSessionPath).Should().BeTrue("the running session is saved");

        ui.Vm.OnWindowRegainedFocus();
        ui.Vm.HasActiveSession.Should().BeTrue("focus no longer ends tracked sessions");

        now = now.AddMinutes(12);
        ui.Vm.PollSession();
        scanner.Stop(10);
        now = now.AddSeconds(10);
        ui.Vm.PollSession();

        ui.Vm.HasActiveSession.Should().BeFalse();
        PlayTimeService.GetTotal("Epic Games", id).Should().Be(TimeSpan.FromMinutes(12));
        File.Exists(ui.Vm.ActiveSessionPath).Should().BeFalse("a finished session leaves nothing to recover");
    }

    [AvaloniaFact]
    public void GameThatNeverStarts_ShowsAClearMessage_AndCountsNothing()
    {
        var id = "never" + Guid.NewGuid().ToString("N")[..6];
        var game = EpicGame(id);
        Directory.CreateDirectory(game.InstallPath);
        using var ui = new UiHarness(language: Loc.English, games: new[] { game });
        var card = ui.Vm.Games.Single(g => g.Game.Id == id);

        var now = DateTime.UtcNow;
        var tracker = FolderSessionTracker.TryCreate(game.InstallPath, new FakeScanner(), () => now)!;
        ui.Vm.BeginPlaySession(card, tracker);
        now = now.AddMinutes(4);
        ui.Vm.PollSession();

        ui.Vm.HasActiveSession.Should().BeFalse();
        ui.Vm.StatusMessage.Should().Contain("didn't start within 3 minutes");
        PlayTimeService.GetTotal("Epic Games", id).Should().Be(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void GamesSharingAFolder_AreAmbiguous_AndNotTracked()
    {
        var a = EpicGame("amb-a", Path.Combine(_dir, "shared"));
        var b = EpicGame("amb-b", Path.Combine(_dir, "shared", "sub"));
        var c = EpicGame("amb-c", Path.Combine(_dir, "alone"));
        Directory.CreateDirectory(b.InstallPath);
        Directory.CreateDirectory(c.InstallPath);
        using var ui = new UiHarness(games: new[] { a, b, c });
        ui.Vm.ProcessScanner = new FakeScanner();

        ui.Vm.CreateTracker(ui.Vm.Games.Single(g => g.Game.Id == "amb-a").Game).Ambiguous.Should().BeTrue();
        ui.Vm.CreateTracker(ui.Vm.Games.Single(g => g.Game.Id == "amb-b").Game).Ambiguous.Should().BeTrue();
        var alone = ui.Vm.CreateTracker(ui.Vm.Games.Single(g => g.Game.Id == "amb-c").Game);
        alone.Ambiguous.Should().BeFalse();
        alone.Tracker.Should().BeOfType<FolderSessionTracker>();
    }

    [AvaloniaFact]
    public async Task SavedSessionOfAClosedGame_IsCreditedOnStartup()
    {
        var id = "rec" + Guid.NewGuid().ToString("N")[..6];
        using var ui = new UiHarness(games: new[] { EpicGame(id) });
        ui.Vm.ActiveSessionPath = Path.Combine(_dir, "active-session.json");
        ui.Vm.ProcessScanner = new FakeScanner();

        var now = DateTime.UtcNow;
        ActiveSessionStore.Save(ui.Vm.ActiveSessionPath, new ActiveSessionRecord
        {
            Platform = "Epic Games", Id = id, Name = "Epic " + id, Kind = "folder",
            InstallDir = Path.Combine(_dir, id), StartedUtc = now.AddMinutes(-30), LastSeenUtc = now.AddMinutes(-5)
        });

        await ui.Vm.RecoverActiveSessionAsync();

        PlayTimeService.GetTotal("Epic Games", id).Should().Be(TimeSpan.FromMinutes(25));
        File.Exists(ui.Vm.ActiveSessionPath).Should().BeFalse();
        ui.Vm.HasActiveSession.Should().BeFalse();
    }

    [AvaloniaFact]
    public void LaunchProblemMessages_ExistInBothLanguages()
    {
        foreach (var key in new[] { "launchDriveMissing", "launchFilesMissing", "launchLauncherMissing",
                     "launchSteamSignedOut", "launchTimeout", "launchAlreadyRunning" })
        {
            Loc.Lookup(Loc.Arabic, key).Should().NotBeNullOrEmpty(key);
            Loc.Lookup(Loc.English, key).Should().NotBeNullOrEmpty(key);
        }
        MainViewModel.LaunchProblemMessage(LaunchProblem.None, "x", "").Should().BeNull();
    }
}
