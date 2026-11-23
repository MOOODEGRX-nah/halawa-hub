using Avalonia.Headless.XUnit;
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
    public event Action<BpNotification>? NotificationArrived;
    public event Action<bool>? DataChanged;
    public void Rescan(params BpGame[] added) { GameList.AddRange(added); DataChanged?.Invoke(true); }
    public void Arrive(BpNotification n) { NotificationList.Insert(0, n); NotificationArrived?.Invoke(n); }
    public IReadOnlyList<BpNotification> Notifications => NotificationList;
    public void DismissNotification(BpNotification item) => NotificationList.Remove(item);
    public void ClearNotifications() => NotificationList.Clear();
    public void MarkNotificationsRead() { foreach (var n in NotificationList) n.Unread = false; }
    public void ExitBigPicture() => Calls.Add("exit");
    public void CloseApp() => Calls.Add("close");
}

/// Loc تُغيَّر هنا، ونوافذ اختبارات سابقة تستمع لتغيير اللغة: على خيط الواجهة فقط، وإلا بدأت حركات
/// خارج خيطها وتعطّل رسم الاختبارات التالية في CI
public class BigPictureNavigationTests : IDisposable
{
    private static readonly Func<TimeSpan, Task> NoDelay = _ => Task.CompletedTask;

    public BigPictureNavigationTests() => Loc.Instance.Language = Loc.English;
    public void Dispose() => Loc.Instance.Language = Loc.Arabic;

    private static BigPictureViewModel Vm(FakeBpHost host) => new(host, NoDelay);

    [AvaloniaFact]
    public void Home_starts_on_the_first_card_and_moves_up_to_the_strip_and_hero_buttons()
    {
        var vm = Vm(FakeBpHost.WithGames(10));

        vm.FocusKey.Should().Be("card.0");
        vm.Press(BpButton.Up);
        vm.FocusKey.Should().Be("strip.0");
        vm.Press(BpButton.Up);
        vm.FocusKey.Should().Be("hero.0");
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void Cards_end_with_see_all_which_opens_the_library()
    {
        var vm = Vm(FakeBpHost.WithGames(3));
        for (var i = 0; i < 5; i++) vm.Press(BpButton.Right);

        vm.FocusKey.Should().Be("more");
        vm.Press(BpButton.A);
        vm.Screen.Should().Be(BpScreen.Library);
    }

    [AvaloniaFact]
    public void Focused_card_becomes_the_hero()
    {
        var host = FakeBpHost.WithGames(5);
        var vm = Vm(host);
        vm.Press(BpButton.Right);

        vm.Hero.Should().BeSameAs(vm.RowCards[1].Game);
        vm.RowCards[1].IsFocused.Should().BeTrue();
        vm.RowCards[0].IsFocused.Should().BeFalse();
    }

    [AvaloniaFact]
    public void Arabic_swaps_left_and_right()
    {
        Loc.Instance.Language = Loc.Arabic;
        var vm = Vm(FakeBpHost.WithGames(5));

        vm.Press(BpButton.Left);
        vm.FocusKey.Should().Be("card.1");
        vm.Press(BpButton.Right);
        vm.FocusKey.Should().Be("card.0");
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void Library_grid_has_seven_columns_and_does_not_wrap_rows()
    {
        var vm = Vm(FakeBpHost.WithGames(20));
        vm.GoTab(BpScreen.Library);

        for (var i = 0; i < 10; i++) vm.Press(BpButton.Right);
        vm.LibraryIndex.Should().Be(6);
        vm.Press(BpButton.Down);
        vm.LibraryIndex.Should().Be(13);
        vm.Press(BpButton.Down);
        vm.LibraryIndex.Should().Be(19, "the last row is shorter: Down lands on its last game");
        vm.Press(BpButton.A);
        vm.Screen.Should().Be(BpScreen.Game);
        vm.Press(BpButton.B);
        vm.Screen.Should().Be(BpScreen.Library);
    }

    [AvaloniaFact]
    public void Up_from_the_first_library_row_reaches_the_platform_bar()
    {
        var host = FakeBpHost.WithGames(6, i => FakeBpHost.Game(i, platform: i % 2 == 0 ? "Steam" : "Epic"));
        var vm = Vm(host);
        vm.GoTab(BpScreen.Library);

        vm.Press(BpButton.Up);
        vm.FocusKey.Should().Be("flt.all");
        vm.Press(BpButton.Right);
        vm.FocusKey.Should().Be("flt.Steam");
        vm.LibraryCards.Should().OnlyContain(c => c.Game.Platform == "Steam");
        vm.Press(BpButton.Down);
        vm.FocusKey.Should().Be("lib.0");
        vm.Press(BpButton.Up);
        vm.Press(BpButton.A);
        vm.FocusKey.Should().Be("lib.0", "A on the bar goes back to the games");
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void Search_with_no_match_says_so()
    {
        var vm = Vm(FakeBpHost.WithGames(3));
        vm.GoTab(BpScreen.Search);
        vm.SetQuery("zzq");

        vm.NoMatch.Should().BeTrue();
        vm.NoMatchText.Should().Contain("zzq");
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void Launch_spinner_is_idle_unless_the_launch_panel_is_open()
    {
        // regression: the hidden panel kept its pending spinner looping and used CPU while idle
        var host = FakeBpHost.WithGames(3);
        host.PendingLaunch = new TaskCompletionSource<BpLaunchResult>();
        var vm = Vm(host);
        vm.LaunchBadge.Should().Be(App.Controls.BadgeState.None);

        vm.Play(vm.RowCards[0].Game);
        vm.LaunchBadge.Should().Be(App.Controls.BadgeState.Pending);
        vm.Press(BpButton.B);
        vm.LaunchBadge.Should().Be(App.Controls.BadgeState.None);
        host.PendingLaunch.SetResult(BpLaunchResult.Success);
    }

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
    public void A_new_notification_pops_up_shakes_the_bell_and_opens_the_panel()
    {
        var host = FakeBpHost.WithGames(3);
        var gate = new TaskCompletionSource();
        var vm = new BigPictureViewModel(host, _ => gate.Task);

        host.Arrive(new BpNotification { Id = "n1", Kind = "free", Title = "Free this week on Epic", Unread = true });

        vm.Incoming!.Id.Should().Be("n1");
        vm.BellVersion.Should().Be(1);
        vm.UnreadCount.Should().Be(1);
        vm.OpenIncoming();
        vm.Overlay.Should().Be(BpOverlay.Notifications);
        vm.Incoming.Should().BeNull();
        vm.NotifItems[0].IsNew.Should().BeTrue();
        gate.SetResult();
    }

    [AvaloniaFact]
    public void Notification_actions_update_retry_and_show()
    {
        var host = FakeBpHost.WithGames(3);
        var g = host.GameList[1];
        host.NotificationList.Add(new BpNotification { Id = "u", Kind = "upd", Title = "u", ActionId = "update", ActionLabel = "Update in Steam", Game = g });
        host.NotificationList.Add(new BpNotification { Id = "f", Kind = "fail", Title = "f", ActionId = "retry", ActionLabel = "Try again", Game = g });
        host.NotificationList.Add(new BpNotification { Id = "s", Kind = "scan", Title = "s", ActionId = "show", ActionLabel = "Show" });
        var vm = Vm(host);

        vm.OpenNotifications();
        vm.Press(BpButton.A);
        host.Calls.Should().Contain("update:" + g.Key);
        vm.Overlay.Should().Be(BpOverlay.None);

        vm.OpenNotifications();
        vm.Press(BpButton.Down);
        vm.Press(BpButton.A);
        host.Calls.Should().Contain("launch:" + g.Key);

        vm.OpenNotifications();
        vm.Press(BpButton.Down);
        vm.Press(BpButton.Down);
        vm.Press(BpButton.A);
        vm.Screen.Should().Be(BpScreen.Library);
    }

    [AvaloniaFact]
    public void Notification_times_read_like_the_design()
    {
        var now = new DateTime(2026, 11, 20, 15, 0, 0);
        BigPictureHost.RelativeTime(now.AddSeconds(-20), now).Should().Be("Now");
        BigPictureHost.RelativeTime(now.AddMinutes(-2), now).Should().Be("2 min ago");
        BigPictureHost.RelativeTime(now.AddMinutes(-70), now).Should().Be("1 hour ago");
        BigPictureHost.RelativeTime(now.AddDays(-1), now).Should().Be("Yesterday");
        Loc.Instance.Language = Loc.Arabic;
        BigPictureHost.RelativeTime(now.AddMinutes(-2), now).Should().Be("قبل دقيقتين");
    }

    [AvaloniaFact]
    public void A_library_rescan_while_open_shows_new_games_and_keeps_the_hero()
    {
        var host = FakeBpHost.WithGames(3);
        var vm = Vm(host);
        vm.Press(BpButton.Right);
        var hero = vm.Hero;

        host.Rescan(FakeBpHost.Game(9));

        vm.LibraryCards.Should().HaveCount(4);
        vm.Hero.Should().BeSameAs(hero);
        vm.FocusKey.Should().Be("card.1");
    }

    [AvaloniaFact]
    public void Auto_button_icons_follow_the_connected_controller()
    {
        var host = FakeBpHost.WithGames(2);
        var vm = Vm(host);
        vm.UsePsGlyphs.Should().BeFalse();
        vm.SetPad(new PadInfo("DualSense", PadKind.PlayStation, 60));
        vm.UsePsGlyphs.Should().BeTrue();
        vm.BatteryBars.Should().Be(3);
        vm.ContextText.Should().Be("Controller connected");
        vm.SetPad(new PadInfo("Xbox", PadKind.Xbox, -1));
        vm.UsePsGlyphs.Should().BeFalse();
        vm.HasBattery.Should().BeFalse();
        host.Settings.Glyphs = "ps";
        vm.UsePsGlyphs.Should().BeTrue("a fixed choice wins over the connected controller");
        vm.SetPad(null);
        vm.ContextText.Should().Be("Keyboard");
    }

    [AvaloniaFact]
    public void An_empty_library_does_not_crash_any_screen()
    {
        var vm = Vm(new FakeBpHost());
        vm.FocusKey.Should().Be("more");
        foreach (var b in new[] { BpButton.Up, BpButton.A, BpButton.Up, BpButton.A, BpButton.RB, BpButton.A, BpButton.X, BpButton.RB, BpButton.A, BpButton.Menu, BpButton.Down, BpButton.A })
            vm.Press(b);
        vm.Hero.Should().BeNull();
    }

    [AvaloniaFact]
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
    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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

    [AvaloniaFact]
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
