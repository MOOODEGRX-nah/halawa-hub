using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.BigPicture.Controls;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>
/// نافذة الشاشة الكبيرة الحقيقية ببيانات التصميم. مع HALAWAHUB_SHOTS_DIR تُحفظ الصور
/// (bp-ar-01-home.png ...) للمقارنة مع bigpicture-handoff/screens.
/// </summary>
public class BigPictureUiTests : IDisposable
{
    private static readonly string? ShotsDir = Environment.GetEnvironmentVariable("HALAWAHUB_SHOTS_DIR");

    public BigPictureUiTests() => MotionSettings.Enabled = false;
    public void Dispose()
    {
        MotionSettings.Enabled = true;
        Loc.Instance.Language = Loc.Arabic;
    }

    internal static FakeBpHost DesignHost()
    {
        var host = new FakeBpHost();
        var now = DateTime.Now;
        (string name, string plat, double h, int days, string size, bool upd)[] games =
        {
            ("Elden Ring", "Steam", 142, 1, "49.6 GB", true), ("Cyberpunk 2077", "GOG", 86, 3, "68.4 GB", false),
            ("Hades II", "Steam", 31, 8, "9.8 GB", false), ("Alan Wake 2", "Epic", 19, 15, "72.1 GB", false),
            ("Forza Horizon 5", "Xbox", 54, 40, "110 GB", false), ("Valorant", "Riot", 212, 3, "31.2 GB", false),
            ("Baldur's Gate 3", "Steam", 97, 45, "122 GB", false), ("Celeste", "Steam", 12, 70, "1.2 GB", false),
            ("Stardew Valley", "GOG", 64, 16, "0.6 GB", false), ("Hollow Knight", "Steam", 40, 75, "9.1 GB", false),
            ("Sea of Thieves", "Xbox", 23, 42, "84.5 GB", false), ("Fortnite", "Epic", 75, 9, "37.4 GB", true),
            ("League of Legends", "Riot", 310, 17, "22.8 GB", false), ("Red Dead Redemption 2", "Epic", 58, 76, "119 GB", false),
        };
        for (var i = 0; i < games.Length; i++)
        {
            var g = games[i];
            host.GameList.Add(new BpGame
            {
                Key = "k" + i, Name = g.name, Platform = g.plat, PlatformShort = g.plat, Hours = g.h,
                LastPlayed = now.AddDays(-g.days), DateAdded = now.AddDays(-30 + i), SizeText = g.size, HasUpdate = g.upd,
                IsFavorite = i is 2 or 3 or 7 or 8 or 9 or 10, SteamAppId = g.plat == "Steam" ? (1000 + i).ToString() : null,
            });
        }
        return host;
    }

    private static (BigPictureWindow Window, BigPictureViewModel Vm) Open(string language, FakeBpHost? host = null)
    {
        Loc.Instance.Language = language;
        var vm = new BigPictureViewModel(host ?? DesignHost(), _ => Task.CompletedTask);
        var window = new BigPictureWindow(vm, null) { Width = 1920, Height = 1080 };
        window.Show();
        Settle();
        return (window, vm);
    }

    private static void Settle()
    {
        for (var i = 0; i < 8; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window window, string language, string name)
    {
        Settle();
        using var frame = window.CaptureRenderedFrame();
        frame.Should().NotBeNull();
        if (string.IsNullOrEmpty(ShotsDir)) return;
        Directory.CreateDirectory(ShotsDir);
        frame!.Save(Path.Combine(ShotsDir, $"bp-{language}-{name}.png"));
    }

    private static Border Ring(Window w) => w.GetVisualDescendants().OfType<Border>().First(b => b.Name == "Ring");

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void Home_renders_with_the_ring_around_the_focused_card(string language)
    {
        var (window, vm) = Open(language);

        vm.FocusKey.Should().Be("card.0");
        var ring = Ring(window);
        ring.Opacity.Should().Be(1);
        var rect = window.FocusRect();
        rect.Should().NotBeNull();
        Canvas.GetLeft(ring).Should().BeApproximately(rect!.Value.X - 7, 0.5);
        Canvas.GetTop(ring).Should().BeApproximately(rect.Value.Y - 7, 0.5);
        Shot(window, language, "02-home-row");

        vm.Press(App.BigPicture.Input.BpButton.Up);
        vm.Press(App.BigPicture.Input.BpButton.Up);
        Settle();
        vm.FocusKey.Should().Be("hero.0");
        Shot(window, language, "01-home");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void Every_screen_renders_and_the_ring_finds_its_target(string language)
    {
        var host = DesignHost();
        host.News.Add(("[News headline 1]", "2026/11/18", "https://example.invalid/1"));
        host.News.Add(("[News headline 2]", "2026/11/12", "https://example.invalid/2"));
        var (window, vm) = Open(language, host);
        void Check(string name, bool ring = true)
        {
            Settle();
            if (ring) window.FocusRect().Should().NotBeNull($"the ring needs a target on {name} ({vm.FocusKey})");
            Shot(window, language, name);
        }

        vm.GoTab(BpScreen.Library);
        Check("03-library");

        var elden = host.GameList[0];
        vm.OpenGame(elden);
        Check("04-game");

        vm.Play(elden);
        Check("05-update-prompt");
        vm.CloseOverlay();

        host.PendingLaunch = new TaskCompletionSource<BpLaunchResult>();
        vm.Play(host.GameList[1]);
        Check("06-launching", ring: false);
        vm.CloseOverlay();
        // xUnit ينتظر كل async void: التشغيل المعلّق يجب أن ينتهي
        host.PendingLaunch.SetResult(BpLaunchResult.Success);

        host.PendingLaunch = null;
        host.NextLaunch = new BpLaunchResult(false, Loc.Instance["bpWhySteam"], "Steam is not signed in. Sign in to Steam, then try again.", true);
        vm.OpenGame(elden);
        vm.Press(App.BigPicture.Input.BpButton.Right);
        vm.Press(App.BigPicture.Input.BpButton.Left);
        vm.ClickUpdateButton(1);
        vm.ClickUpdateButton(1);
        Check("07-launch-failed");
        vm.CloseOverlay();

        vm.GoTab(BpScreen.Search);
        vm.SetQuery("el");
        vm.Press(App.BigPicture.Input.BpButton.Up);
        Check("08-search");
        vm.SetQuery("zzq");
        Check("09-search-no-match");

        vm.GoTab(BpScreen.Home);
        vm.ToggleGuide();
        Check("10-menu", ring: false);
        vm.CloseOverlay();

        vm.OpenSettings();
        Check("11-settings-general", ring: false);
        host.Settings.Glyphs = "ps";
        vm.ClickSection(1);
        Check("12-settings-controller-ps", ring: false);
        vm.Press(vm.IsRtl ? App.BigPicture.Input.BpButton.Left : App.BigPicture.Input.BpButton.Right);
        Check("12b-settings-row");
        window.Close();
    }

    [AvaloniaFact]
    public void Library_of_1000_games_builds_only_the_visible_rows()
    {
        var host = FakeBpHost.WithGames(1000);
        var (window, vm) = Open(Loc.English, host);
        vm.GoTab(BpScreen.Library);
        Settle();

        var cards = window.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("bpcard") && (BpFocus.GetKey(b)?.StartsWith("lib.") ?? false));
        cards.Should().BeLessThan(100);

        for (var i = 0; i < 30; i++) vm.Press(App.BigPicture.Input.BpButton.Down);
        Settle();
        vm.LibraryIndex.Should().Be(210);
        window.FocusRect().Should().NotBeNull("the focused row is scrolled into view");
        window.Close();
    }

    [AvaloniaFact]
    public void Arrow_keys_move_the_focus_and_escape_on_home_does_nothing()
    {
        var (window, vm) = Open(Loc.English);

        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        vm.FocusKey.Should().Be("card.1");
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        vm.Screen.Should().Be(BpScreen.Home);
        window.IsVisible.Should().BeTrue();
        window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
        vm.Screen.Should().Be(BpScreen.Library);
        window.Close();
    }

    [AvaloniaFact]
    public void Focus_keys_are_unique_on_the_home_screen()
    {
        var (window, _) = Open(Loc.English);
        var keys = window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && BpFocus.GetKey(c) != null)
            .Select(c => BpFocus.GetKey(c)).ToList();

        keys.Should().OnlyHaveUniqueItems();
        keys.Should().Contain(new[] { "hero.0", "strip.3", "card.0", "more" });
        window.Close();
    }
}
