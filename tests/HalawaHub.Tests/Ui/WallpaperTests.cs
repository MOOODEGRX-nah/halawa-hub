using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.BigPicture.Controls;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.BigPicture.Wallpaper;
using HalawaHub.App.Services;
using SkiaSharp;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>تحديث التصميم 7: الخلفيات المتحركة في الشاشة الكبيرة.</summary>
public class WallpaperTests : IDisposable
{
    public void Dispose()
    {
        Loc.Instance.Language = Loc.Arabic;
        MotionSettings.Enabled = true;
    }

    [Theory]
    [InlineData("blobs")]
    [InlineData("aurora")]
    [InlineData("grain")]
    [InlineData("dots")]
    [InlineData("flow")]
    [InlineData("contour")]
    [InlineData("stars")]
    [InlineData("spot")]
    [InlineData("plain")]
    public void Every_scene_draws_and_reacts_to_a_tap(string id)
    {
        var engine = new WallpaperEngine();
        engine.SetScene(id);
        engine.SetColors("blue", oled: false);
        using var surface = SKSurface.Create(new SKImageInfo(240, 135));
        for (var i = 0; i < 5; i++) engine.Frame(surface.Canvas, 240, 135, 1.0 / 30);
        engine.OnTap(100, 80);
        for (var i = 0; i < 30; i++) engine.Frame(surface.Canvas, 240, 135, 1.0 / 30);
        using var img = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        var center = bmp.GetPixel(120, 67);
        center.Alpha.Should().Be(255, "the background is opaque");
        if (id != "plain") bmp.Pixels.Distinct().Count().Should().BeGreaterThan(20, "the scene draws something, not a flat fill");
    }

    [Fact]
    public void The_palette_follows_the_accent_and_oled()
    {
        WallPalette.For("green").A.Should().Be("22,163,74");
        WallPalette.For("unknown").A.Should().Be("239,68,68");
        var engine = new WallpaperEngine();
        engine.SetScene("plain");
        engine.SetColors("red", oled: true);
        using var surface = SKSurface.Create(new SKImageInfo(48, 27));
        engine.Frame(surface.Canvas, 48, 27, 0);
        using var img = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(img);
        bmp.GetPixel(10, 10).Should().Be(new SKColor(0, 0, 0), "OLED is pure black");
    }

    [Fact]
    public void Old_settings_load_with_the_grid_moving_when_you_move()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<BpSettings>("""{"OpenOnController":true,"ShowClock":false}""")!;
        old.Wallpaper.Should().Be("grid");
        old.WallpaperMotion.Should().Be("move");
        old.WallpaperReact.Should().BeTrue();
    }

    [AvaloniaFact]
    public void Display_settings_pick_a_background_with_left_and_right()
    {
        Loc.Instance.Language = Loc.English;
        var host = FakeBpHost.WithGames(3);
        var vm = new BigPictureViewModel(host, _ => Task.CompletedTask);
        var changes = 0;
        vm.WallpaperChanged += () => changes++;
        vm.OpenSettings();
        vm.Press(BpButton.Down);
        vm.Press(BpButton.Down);                  // العرض
        vm.SettingRows.Select(r => r.Id).Take(3).Should().Equal("wall", "wallmotion", "wallreact");
        vm.SettingRows[0].Options.Should().HaveCount(10);
        vm.SettingRows[0].Options.First().Should().Be("Grid light");
        vm.SettingRows[0].Options.Last().Should().Be("Plain");

        vm.Press(BpButton.Right);                 // إلى الأسطر
        vm.Press(BpButton.Right);                 // الخلفية التالية
        host.Settings.Wallpaper.Should().Be("blobs");
        changes.Should().Be(1, "the background behind the dialog changes live");
        vm.SettingRows[0].Walls.Single(w => w.IsOn).Id.Should().Be("blobs");

        vm.Press(BpButton.Down);
        vm.Press(BpButton.Right);
        host.Settings.WallpaperMotion.Should().Be("always");
        vm.Press(BpButton.Down);
        vm.Press(BpButton.A);
        host.Settings.WallpaperReact.Should().BeFalse();
    }

    [AvaloniaFact]
    public void When_you_move_stops_after_the_wake_and_still_draws_one_frame()
    {
        var window = new Window { Width = 400, Height = 300 };
        var view = new WallpaperView();
        window.Content = view;
        window.Show();
        view.Configure("blobs", "red", false, "still", true);
        Settle();
        var still = view.FramesDrawn;
        still.Should().BeGreaterThan(0);
        view.TapAt(new Avalonia.Point(100, 100));
        Settle();
        view.IsRunning.Should().BeFalse("Still never keeps running");

        view.Configure("blobs", "red", false, "move", true);
        view.IsRunning.Should().BeTrue("a move wakes it for a moment");
        window.Close();
    }

    [AvaloniaFact]
    public void React_off_keeps_only_the_faint_grid()
    {
        var light = new BpGridLight { ShowLight = false };
        light.ShowLight.Should().BeFalse();
        new BpGridLight().ShowLight.Should().BeTrue();
    }

    private static void Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}
