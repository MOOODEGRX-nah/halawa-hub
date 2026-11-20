using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests.Ui;

internal sealed class FakePads : IGamepadSource
{
    public event Action<BpButton>? ButtonPressed;
    public event Action<PadInfo?>? PadChanged;
    public PadInfo? Current { get; set; }
    public bool Active { get; private set; }
    public void SetActive(bool active) => Active = active;
    public void Connect(PadInfo? pad) { Current = pad; PadChanged?.Invoke(pad); }
    public void Press(BpButton b) => ButtonPressed?.Invoke(b);
    public void Dispose() { }
}

/// <summary>المرحلة 5: الدخول إلى الشاشة الكبيرة والخروج منها، وإعداداتها في config.json.</summary>
public class BigPictureEntryTests
{
    [Fact]
    public void Old_config_files_load_big_picture_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "halawa-bp-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"Language\":\"en\",\"Theme\":\"dark\"}");
        try
        {
            var cfg = ConfigService.LoadFrom(path);
            cfg.BigPicture.Should().NotBeNull();
            cfg.BigPicture.OpenOnController.Should().BeTrue();
            cfg.BigPicture.StartInBigPicture.Should().BeFalse();
            cfg.BigPicture.ExitOnDisconnect.Should().BeTrue();
            cfg.BigPicture.Glyphs.Should().Be("auto");
            cfg.BigPicture.TextSize.Should().Be("normal");
            cfg.BigPicture.ShowClock.Should().BeTrue();

            cfg.BigPicture.Glyphs = "ps";
            cfg.BigPicture.SwapConfirmBack = true;
            ConfigService.SaveTo(path, cfg);
            var again = ConfigService.LoadFrom(path);
            again.BigPicture.Glyphs.Should().Be("ps");
            again.BigPicture.SwapConfirmBack.Should().BeTrue();
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public void Top_bar_button_opens_big_picture_and_exit_returns_to_the_normal_window()
    {
        using var ui = new UiHarness(Loc.English);
        var pads = new FakePads();
        var controller = new BigPictureController(ui.Window, ui.Vm, () => pads);

        var button = ui.Window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "BigPictureButton");
        button.Command!.Execute(null);
        ui.Settle();

        controller.IsOpen.Should().BeTrue();
        ui.Window.IsVisible.Should().BeFalse("the normal window hides behind Big Picture");
        pads.Active.Should().BeTrue();

        controller.Exit();
        ui.Settle();
        controller.IsOpen.Should().BeFalse();
        ui.Window.IsVisible.Should().BeTrue();
        controller.Dispose();
    }

    [AvaloniaFact]
    public void Connecting_a_controller_opens_big_picture_but_pads_present_at_start_do_not()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.Config.BigPicture.OpenOnController = true;
        var pads = new FakePads();
        var controller = new BigPictureController(ui.Window, ui.Vm, () => pads) { Grace = TimeSpan.FromHours(1) };

        controller.EnsureWatching();
        pads.Connect(new PadInfo("Xbox Controller", PadKind.Xbox, 80));
        ui.Settle();
        controller.IsOpen.Should().BeFalse("a pad that was already connected when watching started must not open it");

        pads.Connect(null);
        controller.Grace = TimeSpan.Zero;
        pads.Connect(new PadInfo("DualSense", PadKind.PlayStation, -1));
        ui.Settle();
        controller.IsOpen.Should().BeTrue();

        controller.Exit();
        ui.Settle();
        controller.Dispose();
    }

    [AvaloniaFact]
    public void Turning_off_open_on_connect_keeps_the_normal_window()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.BigPictureOnController = false;
        var created = 0;
        var controller = new BigPictureController(ui.Window, ui.Vm, () => { created++; return new FakePads(); });

        controller.EnsureWatching();

        created.Should().Be(0, "no controller thread runs when nothing needs it");
        ui.Vm.BigPictureOnController = true;
        created.Should().Be(1);
        controller.Dispose();
    }

    [AvaloniaFact]
    public void Large_text_uses_a_smaller_canvas_so_everything_scales_up()
    {
        var host = FakeBpHost.WithGames(3);
        var vm = new BigPictureViewModel(host, _ => Task.CompletedTask);
        vm.CanvasWidth.Should().Be(1920);
        vm.OpenSettings();
        vm.ClickSection(2);
        vm.ClickRow(0);
        host.Settings.TextSize.Should().Be("large");
        vm.CanvasWidth.Should().Be(1745);
        vm.CanvasHeight.Should().Be(982);
    }
}
