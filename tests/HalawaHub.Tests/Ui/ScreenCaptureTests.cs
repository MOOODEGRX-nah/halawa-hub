using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>
/// يرسم كل شاشة باللغتين. يتأكد أن الرسم ينجح دائمًا، وإذا كان متغير البيئة
/// HALAWAHUB_SHOTS_DIR مضبوطًا يحفظ الصور للمقارنة مع design-handoff/screens.
/// </summary>
public class ScreenCaptureTests
{
    private static readonly string? ShotsDir = Environment.GetEnvironmentVariable("HALAWAHUB_SHOTS_DIR");

    private static void Shot(UiHarness ui, string language, string name)
    {
        ui.Settle();
        using var frame = ui.Window.CaptureRenderedFrame();
        frame.Should().NotBeNull();

        if (string.IsNullOrEmpty(ShotsDir)) return;
        Directory.CreateDirectory(ShotsDir);
        frame!.Save(Path.Combine(ShotsDir, $"{language}-{name}.png"));
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void EveryScreen_Renders(string language)
    {
        using var ui = new UiHarness(language);
        var vm = ui.Vm;
        var cyberpunk = vm.Games.First(g => g.Game.Id == "cyberpunk");

        Shot(ui, language, "01-library");

        vm.ToggleCardMenu(cyberpunk);
        Shot(ui, language, "02-library-card-menu");
        vm.CloseAllCardMenus();

        vm.GoDeveloperCommand.Execute(null);
        Shot(ui, language, "03-developer-coming-soon");
        vm.GoLibraryCommand.Execute(null);
        ui.Settle();

        vm.CheckForUpdatesNowCommand.Execute(null);
        ui.WaitFor(() => !vm.IsCheckingForUpdates);
        vm.ApiKeyStatus = ApiKeyCheckState.Valid;
        vm.OpenSettingsCommand.Execute(null);
        vm.SelectSettingsSectionCommand.Execute("0");

        var settings = new[] { "05-settings-updates", "06-settings-language", "07-settings-api", "08-settings-appearance", "09-settings-startup", "10-settings-build" };
        for (var i = 0; i < settings.Length; i++)
        {
            vm.SelectSettingsSectionCommand.Execute(i.ToString());
            Shot(ui, language, settings[i]);
        }
        vm.CloseSettingsCommand.Execute(null);
        ui.Settle();

        vm.OpenDetailsCommand.Execute(cyberpunk);
        var dialog = new[] { "11-game-dialog-general", "12-game-dialog-files", "13-game-dialog-custom", "14-game-dialog-launch" };
        for (var i = 0; i < dialog.Length; i++)
        {
            vm.SelectGameDialogSectionCommand.Execute(i.ToString());
            if (i == 3) cyberpunk.LaunchParameters = "--skip-launcher";
            Shot(ui, language, dialog[i]);
        }
        cyberpunk.LaunchParameters = "";
        vm.CloseDetailsCommand.Execute(null);
        ui.Settle();

        // البحث: مفتوح، اقتراحات، ولا توجد نتائج
        vm.IsSearchOpen = true;
        vm.SearchQuery = "el";
        Shot(ui, language, "20-search-suggestions");
        vm.SearchQuery = "zzz";
        Shot(ui, language, "21-search-no-match");
        vm.CloseSearch();
    }

    /// <summary>
    /// النافذتان العائمتان بكل أقسامهما على أحجام مختلفة للبرنامج: أصغر حجم، مكبّر، وشاشة كبيرة.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(Loc.Arabic, 980, 620)]
    [InlineData(Loc.English, 980, 620)]
    [InlineData(Loc.Arabic, 1920, 1080)]
    [InlineData(Loc.English, 1920, 1080)]
    [InlineData(Loc.Arabic, 2560, 1440)]
    [InlineData(Loc.English, 2560, 1440)]
    public void Dialogs_Render_AtWindowSize(string language, int width, int height)
    {
        using var ui = new UiHarness(language);
        var vm = ui.Vm;
        ui.Window.Width = width;
        ui.Window.Height = height;
        ui.Settle();
        var size = $"{width}x{height}";

        vm.ApiKeyStatus = ApiKeyCheckState.Valid;
        vm.OpenSettingsCommand.Execute(null);
        vm.SelectSettingsSectionCommand.Execute("0");
        for (var i = 0; i < 6; i++)
        {
            vm.SelectSettingsSectionCommand.Execute(i.ToString());
            Shot(ui, language, $"{size}-settings-{i}");
        }
        vm.CloseSettingsCommand.Execute(null);
        ui.Settle();

        var cyberpunk = vm.Games.First(g => g.Game.Id == "cyberpunk");
        vm.OpenDetailsCommand.Execute(cyberpunk);
        for (var i = 0; i < 4; i++)
        {
            vm.SelectGameDialogSectionCommand.Execute(i.ToString());
            Shot(ui, language, $"{size}-game-{i}");
        }
        vm.CloseDetailsCommand.Execute(null);
        ui.Settle();
    }
}
