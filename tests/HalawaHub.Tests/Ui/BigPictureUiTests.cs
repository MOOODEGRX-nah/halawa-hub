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
                IsFavorite = i is 2 or 3 or 7 or 8 or 9 or 10,
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
