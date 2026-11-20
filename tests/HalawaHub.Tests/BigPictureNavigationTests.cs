using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.Services;
using SDL;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>شاشة كبيرة بلا نافذة: بيانات وهمية وتسجيل لما طُلب من البرنامج.</summary>
internal sealed class FakeBpHost : IBpHost
{
    public List<BpGame> GameList { get; } = new();
    public IReadOnlyList<BpGame> Games => GameList;
    public BpSettings Settings { get; } = new();
    public List<string> Calls { get; } = new();
    public BpLaunchResult NextLaunch { get; set; } = BpLaunchResult.Success;
    public TaskCompletionSource<BpLaunchResult>? PendingLaunch { get; set; }
    public List<BpNotification> NotificationList { get; } = new();

    public static FakeBpHost WithGames(int count, Func<int, BpGame>? make = null)
    {
        var host = new FakeBpHost();
        for (var i = 0; i < count; i++)
            host.GameList.Add(make?.Invoke(i) ?? Game(i));
        return host;
    }

    public static BpGame Game(int i, bool update = false, string platform = "Steam") => new()
    {
        Key = "g" + i, Name = $"Game {i:00}", Platform = platform, PlatformShort = platform, Hours = i,
        LastPlayed = DateTime.Now.AddDays(-i), DateAdded = DateTime.Now.AddDays(-100 + i), HasUpdate = update,
    };

    public void SaveSettings() => Calls.Add("save");
    public void SetLanguage(string language) { Calls.Add("lang:" + language); Loc.Instance.Language = language; }
    public void ToggleFavorite(BpGame game) { game.IsFavorite = !game.IsFavorite; Calls.Add("fav:" + game.Key); }
    public Task<BpLaunchResult> LaunchAsync(BpGame game, CancellationToken token)
    {
        Calls.Add("launch:" + game.Key);
        return PendingLaunch?.Task ?? Task.FromResult(NextLaunch);
    }
    public void OpenUpdateInLauncher(BpGame game) => Calls.Add("update:" + game.Key);
    public void OpenLauncher(BpGame game) => Calls.Add("launcher:" + game.Key);
    public void OpenGameSettings(BpGame game) => Calls.Add("settings:" + game.Key);
    public void OpenNews(BpGame game, string url) => Calls.Add("news:" + url);
    public List<(string Title, string Date, string Url)> News { get; } = new();
    public Task<IReadOnlyList<(string Title, string Date, string Url)>> LoadNewsAsync(BpGame game) =>
        Task.FromResult<IReadOnlyList<(string Title, string Date, string Url)>>(News);
    public IReadOnlyList<BpNotification> Notifications => NotificationList;
    public void DismissNotification(BpNotification item) => NotificationList.Remove(item);
    public void ClearNotifications() => NotificationList.Clear();
    public void MarkNotificationsRead() { foreach (var n in NotificationList) n.Unread = false; }
    public void ExitBigPicture() => Calls.Add("exit");
    public void CloseApp() => Calls.Add("close");
}

public class BigPictureNavigationTests : IDisposable
{
    private static readonly Func<TimeSpan, Task> NoDelay = _ => Task.CompletedTask;

    public BigPictureNavigationTests() => Loc.Instance.Language = Loc.English;
    public void Dispose() => Loc.Instance.Language = Loc.Arabic;

    private static BigPictureViewModel Vm(FakeBpHost host) => new(host, NoDelay);

    [Fact]
    public void Home_starts_on_the_first_card_and_moves_up_to_the_strip_and_hero_buttons()
    {
        var vm = Vm(FakeBpHost.WithGames(10));

        vm.FocusKey.Should().Be("card.0");
        vm.Press(BpButton.Up);
        vm.FocusKey.Should().Be("strip.0");
        vm.Press(BpButton.Up);
        vm.FocusKey.Should().Be("hero.0");
    }

    [Fact]
    public void Pushing_against_an_edge_bumps_instead_of_moving()
    {
        var vm = Vm(FakeBpHost.WithGames(3));
        var bumps = new List<(int, int)>();
        vm.Bumped += (dx, dy) => bumps.Add((dx, dy));

        vm.Press(BpButton.Left).Should().BeFalse();
        vm.Press(BpButton.Down).Should().BeFalse();

        bumps.Should().Equal((-1, 0), (0, 1));
        vm.FocusKey.Should().Be("card.0");
    }

    [Fact]
    public void Cards_end_with_see_all_which_opens_the_library()
    {
        var vm = Vm(FakeBpHost.WithGames(3));
        for (var i = 0; i < 5; i++) vm.Press(BpButton.Right);

        vm.FocusKey.Should().Be("more");
        vm.Press(BpButton.A);
        vm.Screen.Should().Be(BpScreen.Library);
    }

    [Fact]
    public void Focused_card_becomes_the_hero()
    {
        var host = FakeBpHost.WithGames(5);
        var vm = Vm(host);
        vm.Press(BpButton.Right);

        vm.Hero.Should().BeSameAs(vm.RowCards[1].Game);
        vm.RowCards[1].IsFocused.Should().BeTrue();
        vm.RowCards[0].IsFocused.Should().BeFalse();
    }

    [Fact]
    public void Arabic_swaps_left_and_right()
    {
        Loc.Instance.Language = Loc.Arabic;
        var vm = Vm(FakeBpHost.WithGames(5));

        vm.Press(BpButton.Left);
        vm.FocusKey.Should().Be("card.1");
        vm.Press(BpButton.Right);
        vm.FocusKey.Should().Be("card.0");
    }

    [Fact]
    public void Strip_tabs_change_the_card_list()
    {
        var host = FakeBpHost.WithGames(6);
        host.GameList[2].IsFavorite = true;
        var vm = Vm(host);

        vm.Press(BpButton.Up);
        vm.Press(BpButton.Right);

        vm.StripIndex.Should().Be(1);
        vm.RowCards.Select(c => c.Game.Key).Should().Equal("g2");
    }

    [Fact]
    public void Shoulder_buttons_cycle_the_tabs_and_b_returns_home_from_the_library()
    {
        var vm = Vm(FakeBpHost.WithGames(3));

        vm.Press(BpButton.RB);
        vm.Screen.Should().Be(BpScreen.Library);
        vm.Press(BpButton.RB);
        vm.Screen.Should().Be(BpScreen.Search);
        vm.Press(BpButton.RB);
        vm.Screen.Should().Be(BpScreen.Home);
        vm.Press(BpButton.LB);
        vm.Screen.Should().Be(BpScreen.Search);

        vm.Press(BpButton.LB);
        vm.Press(BpButton.B);
        vm.Screen.Should().Be(BpScreen.Home);
        vm.Press(BpButton.B);
        vm.Screen.Should().Be(BpScreen.Home, "B on the home screen does nothing");
    }

    [Fact]
    public void Library_grid_has_seven_columns_and_does_not_wrap_rows()
    {
        var vm = Vm(FakeBpHost.WithGames(20));
        vm.GoTab(BpScreen.Library);

        for (var i = 0; i < 10; i++) vm.Press(BpButton.Right);
        vm.LibraryIndex.Should().Be(6);
        vm.Press(BpButton.Down);
        vm.LibraryIndex.Should().Be(13);
        vm.Press(BpButton.Down);
        vm.LibraryIndex.Should().Be(13, "there is no game at 20");
        vm.Press(BpButton.A);
        vm.Screen.Should().Be(BpScreen.Game);
        vm.Press(BpButton.B);
        vm.Screen.Should().Be(BpScreen.Library);
    }

    [Fact]
    public void X_cycles_the_platform_filter()
    {
        var host = FakeBpHost.WithGames(6, i => FakeBpHost.Game(i, platform: i % 2 == 0 ? "Steam" : "Epic"));
        var vm = Vm(host);
        vm.GoTab(BpScreen.Library);

        vm.Filters.Select(f => f.Id).Should().Equal("all", "Steam", "Epic");
        vm.Press(BpButton.X);
        vm.LibraryCards.Should().OnlyContain(c => c.Game.Platform == "Steam");
        vm.Press(BpButton.X);
        vm.LibraryCards.Should().OnlyContain(c => c.Game.Platform == "Epic");
        vm.Press(BpButton.X);
        vm.LibraryCards.Should().HaveCount(6);
    }

    [Fact]
    public void Search_keyboard_types_deletes_and_moves_to_results()
    {
        var vm = Vm(FakeBpHost.WithGames(12));
        vm.Press(BpButton.Y);
        vm.Screen.Should().Be(BpScreen.Search);
        vm.FocusKey.Should().Be("kb.0.0");

        // G (الصف الثاني) ثم A (أول الصف الثاني)
        vm.Press(BpButton.Down);
        for (var i = 0; i < 4; i++) vm.Press(BpButton.Right);
        vm.Press(BpButton.A);
        vm.Query.Should().Be("g");
        vm.Press(BpButton.B);
        vm.Query.Should().BeEmpty();

        vm.SetQuery("game 1");
        vm.Results.Should().HaveCount(2);
        for (var i = 0; i < 12; i++) vm.Press(BpButton.Right);
        vm.FocusKey.Should().Be("res.0");
        vm.Press(BpButton.A);
        vm.Screen.Should().Be(BpScreen.Game);
    }

    [Fact]
    public void Search_with_no_match_says_so()
    {
        var vm = Vm(FakeBpHost.WithGames(3));
        vm.GoTab(BpScreen.Search);
        vm.SetQuery("zzq");

        vm.NoMatch.Should().BeTrue();
        vm.NoMatchText.Should().Contain("zzq");
    }

    [Fact]
    public void Switch_key_changes_the_keyboard_language()
    {
        var vm = Vm(FakeBpHost.WithGames(3));
        vm.GoTab(BpScreen.Search);
        vm.KeyRows[0][0].Label.Should().Be("Q");

        vm.ClickKey(3, 2);

        vm.KeyboardLanguage.Should().Be("ar");
        vm.KeyRows[0][0].Label.Should().Be("ض");
        vm.FocusKey.Should().Be("kb.3.2");
    }

    [Fact]
    public void Playing_a_game_with_an_update_asks_first()
    {
        var host = FakeBpHost.WithGames(3, i => FakeBpHost.Game(i, update: i == 0));
        var vm = Vm(host);
        var g = vm.RowCards[0].Game;

        vm.Play(g);
        vm.Overlay.Should().Be(BpOverlay.Update);
        vm.FocusKey.Should().Be("upd.0");

        vm.Press(BpButton.Right);
        vm.Press(BpButton.A);
        host.Calls.Should().Contain("launch:" + g.Key);
    }

    [Fact]
    public void A_failed_launch_shows_the_real_reason_and_can_retry()
    {
        var host = FakeBpHost.WithGames(3);
        host.NextLaunch = new BpLaunchResult(false, "Steam is not signed in", "Sign in to Steam, then try again.", CanOpenLauncher: true);
        var vm = Vm(host);

        vm.Play(vm.RowCards[0].Game);

        vm.Overlay.Should().Be(BpOverlay.Launch);
        vm.LaunchState.Should().Be(BpLaunchState.Failed);
        vm.LaunchSub.Should().Be("Steam is not signed in");
        vm.FocusKey.Should().Be("fail.0");
        vm.Press(BpButton.Right);
        host.NextLaunch = BpLaunchResult.Success;
        vm.Press(BpButton.A);
        host.Calls.Count(c => c.StartsWith("launch:")).Should().Be(2);
        vm.Overlay.Should().Be(BpOverlay.None, "a successful launch closes the panel");
    }

    [Fact]
    public void B_cancels_a_pending_launch()
    {
        var host = FakeBpHost.WithGames(3);
        host.PendingLaunch = new TaskCompletionSource<BpLaunchResult>();
        var vm = Vm(host);

        vm.Play(vm.RowCards[0].Game);
        vm.LaunchState.Should().Be(BpLaunchState.Starting);
        vm.Hints.Select(h => h.Button).Should().Equal("b");
        vm.Press(BpButton.B);

        vm.Overlay.Should().Be(BpOverlay.None);
        host.PendingLaunch.SetResult(BpLaunchResult.Success);
        vm.Overlay.Should().Be(BpOverlay.None);
    }

    [Fact]
    public void Settings_toggles_save_and_segments_cycle()
    {
        var host = FakeBpHost.WithGames(3);
        var vm = Vm(host);
        vm.OpenSettings();
        vm.FocusKey.Should().Be("sec.0");

        vm.Press(BpButton.A);
        vm.FocusKey.Should().Be("row.0");
        vm.Press(BpButton.A);
        host.Settings.OpenOnController.Should().BeFalse();
        host.Calls.Should().Contain("save");

        vm.Press(BpButton.Left);
        vm.Press(BpButton.Down);
        vm.SectionTitle.Should().Be("Controller");
        vm.Press(BpButton.Right);
        vm.Press(BpButton.Right);
        host.Settings.Glyphs.Should().Be("xbox");
        vm.Press(BpButton.Left);
        vm.Press(BpButton.Left);
        vm.FocusKey.Should().Be("sec.1", "left on the first option goes back to the sections");

        vm.Press(BpButton.B);
        vm.Screen.Should().Be(BpScreen.Home);
    }

    [Fact]
    public void Swap_confirm_and_back_swaps_a_and_b_and_their_hints()
    {
        var host = FakeBpHost.WithGames(3);
        host.Settings.SwapConfirmBack = true;
        var vm = Vm(host);
        vm.GoTab(BpScreen.Library);

        vm.Press(BpButton.A);
        vm.Screen.Should().Be(BpScreen.Home);
        vm.Hints[0].Button.Should().Be("b");
    }

    [Fact]
    public void Menu_opens_the_guide_and_its_items_run()
    {
        var host = FakeBpHost.WithGames(3);
        var vm = Vm(host);

        vm.Press(BpButton.Menu);
        vm.Overlay.Should().Be(BpOverlay.Guide);
        for (var i = 0; i < 5; i++) vm.Press(BpButton.Down);
        vm.FocusKey.Should().Be("guide.5");
        vm.Press(BpButton.A);
        host.Calls.Should().Contain("exit");
    }

    [Fact]
    public void Notifications_dismiss_and_clear()
    {
        var host = FakeBpHost.WithGames(3);
        for (var i = 0; i < 3; i++)
            host.NotificationList.Add(new BpNotification { Id = "n" + i, Kind = "scan", Title = "N" + i, Unread = true });
        var vm = Vm(host);

        vm.Press(BpButton.Notifications);
        vm.Overlay.Should().Be(BpOverlay.Notifications);
        host.NotificationList.Should().OnlyContain(n => !n.Unread, "opening marks them read");
        vm.Press(BpButton.X);
        host.NotificationList.Select(n => n.Id).Should().Equal("n1", "n2");
        vm.Press(BpButton.Y);
        host.NotificationList.Should().BeEmpty();
        vm.Hints.Select(h => h.Button).Should().Equal("b");
    }

    [Fact]
    public void Relative_days_match_the_design_wording()
    {
        var now = new DateTime(2026, 11, 20, 15, 0, 0);
        BpGame.RelativeDay(now.AddDays(-1), now).Should().Be("Yesterday");
        BpGame.RelativeDay(now.AddDays(-3), now).Should().Be("3 days ago");
        BpGame.RelativeDay(now.AddDays(-8), now).Should().Be("Last week");
        BpGame.RelativeDay(now.AddDays(-15), now).Should().Be("2 weeks ago");
        BpGame.RelativeDay(now.AddDays(-40), now).Should().Be("Last month");
        BpGame.RelativeDay(null, now).Should().Be("Not played yet");
    }
}

public class BigPictureInputTests
{
    [Fact]
    public void Direction_repeats_after_a_delay_then_at_a_steady_rate()
    {
        var r = new DirectionRepeater { DelayMs = 400, RateMs = 100 };

        r.DpadDown(BpButton.Right, 0).Should().Equal(BpButton.Right);
        r.Tick(399).Should().BeEmpty();
        r.Tick(400).Should().Equal(BpButton.Right);
        r.Tick(450).Should().BeEmpty();
        r.Tick(500).Should().Equal(BpButton.Right);
        r.DpadUp(BpButton.Right, 510);
        r.Tick(2000).Should().BeEmpty();
    }

    [Fact]
    public void Stick_has_a_deadzone_and_keeps_its_direction_until_released()
    {
        var r = new DirectionRepeater();

        r.Stick(12000, 0, 0).Should().BeEmpty("inside the deadzone");
        r.Stick(20000, 3000, 10).Should().Equal(BpButton.Right);
        r.Stick(12000, 3000, 20).Should().BeEmpty("still held above the release point");
        r.IsHolding.Should().BeTrue();
        r.Stick(5000, 0, 30).Should().BeEmpty();
        r.IsHolding.Should().BeFalse();
        r.Stick(0, -30000, 40).Should().Equal(BpButton.Up);
    }

    [Fact]
    public void Keyboard_keys_follow_the_design()
    {
        KeyboardMap.Map(Avalonia.Input.Key.Enter).Should().Be(BpButton.A);
        KeyboardMap.Map(Avalonia.Input.Key.Space).Should().Be(BpButton.A);
        KeyboardMap.Map(Avalonia.Input.Key.Back).Should().Be(BpButton.B);
        KeyboardMap.Map(Avalonia.Input.Key.Escape).Should().Be(BpButton.B);
        KeyboardMap.Map(Avalonia.Input.Key.Q).Should().Be(BpButton.LB);
        KeyboardMap.Map(Avalonia.Input.Key.E).Should().Be(BpButton.RB);
        KeyboardMap.Map(Avalonia.Input.Key.Home).Should().Be(BpButton.Menu);
        KeyboardMap.Map(Avalonia.Input.Key.N).Should().Be(BpButton.Notifications);
        KeyboardMap.Map(Avalonia.Input.Key.Z).Should().BeNull();
    }

    [Fact]
    public void Controller_buttons_and_types_map_by_position()
    {
        SdlGamepadSource.MapButton(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH).Should().Be(BpButton.A);
        SdlGamepadSource.MapButton(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST).Should().Be(BpButton.B);
        SdlGamepadSource.MapButton(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE).Should().Be(BpButton.Menu);
        SdlGamepadSource.MapButton(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER).Should().Be(BpButton.RB);
        SdlGamepadSource.KindOf(SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5).Should().Be(PadKind.PlayStation);
        SdlGamepadSource.KindOf(SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE).Should().Be(PadKind.Xbox);
        SdlGamepadSource.KindOf(SDL_GamepadType.SDL_GAMEPAD_TYPE_STANDARD).Should().Be(PadKind.Generic);
    }
}
