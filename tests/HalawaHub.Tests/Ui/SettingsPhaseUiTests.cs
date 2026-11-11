using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using Xunit;

namespace HalawaHub.Tests.Ui;

public class SettingsPhaseUiTests
{
    private static Color ResourceColor(string key) =>
        ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;

    [AvaloniaFact]
    public void Theme_ChangesRightAway_AndIsSaved()
    {
        using var ui = new UiHarness(Loc.English);
        try
        {
            ui.Vm.SettingChoiceCommand.Execute("theme:light");
            ResourceColor("PageBrush").Should().Be(Color.Parse("#F6F2F2"));
            ConfigService.Load().Theme.Should().Be("light");
            ui.Vm.ThemeIndex.Should().Be(2);

            ui.Vm.SettingChoiceCommand.Execute("accent:blue");
            ResourceColor("RedBrush").Should().Be(Color.Parse("#2563EB"));
        }
        finally
        {
            ui.Vm.SettingChoiceCommand.Execute("theme:dark");
            ui.Vm.SettingChoiceCommand.Execute("accent:red");
        }
        ResourceColor("PageBrush").Should().Be(Color.Parse("#0A0404"));
        ResourceColor("RedBrush").Should().Be(Color.Parse("#DC2626"));
    }

    [AvaloniaFact]
    public void Sections_KeepTheirIds_ButShowInTheNewOrder()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.OpenSettingsCommand.Execute(null);
        ui.Settle();

        ui.Vm.SettingsSection.Should().Be(MainViewModel.SectionGeneral, "settings open on General");
        ui.Vm.SettingsPillIndex.Should().Be(0);
        ui.Vm.SettingsTitle.Should().Be("General");

        ui.Vm.SelectSettingsSectionCommand.Execute(MainViewModel.SectionUpdates.ToString());
        ui.Vm.SettingsPillIndex.Should().Be(System.Array.IndexOf(MainViewModel.SettingsOrder, MainViewModel.SectionUpdates));
        ui.Vm.SelectSettingsSectionCommand.Execute(MainViewModel.SectionTools.ToString());
        ui.Vm.SettingsTitle.Should().Be("Tools");
        ui.ButtonWithText("Delete all Halawa data").Should().NotBeNull();
        ui.Vm.CloseSettingsCommand.Execute(null);
    }

    [AvaloniaFact]
    public void CardSize_And_ListLayout_ChangeTheLibrary()
    {
        using var ui = new UiHarness(Loc.Arabic);
        try
        {
            var grid = ui.Window.GetVisualDescendants().OfType<AutoFillGrid>().First();
            grid.MinCell.Should().Be(170);

            ui.Vm.SettingChoiceCommand.Execute("cardsize:large");
            ui.Settle();
            grid.MinCell.Should().Be(220);

            ui.Vm.SettingChoiceCommand.Execute("layout:list");
            ui.Settle();
            ui.Window.GetVisualDescendants().OfType<StackPanel>().Single(s => s.Name == "GamesRows").IsVisible.Should().BeTrue();
            ui.Window.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "GamesList").IsVisible.Should().BeFalse();
        }
        finally
        {
            ui.Vm.SettingChoiceCommand.Execute("cardsize:comfortable");
            ui.Vm.SettingChoiceCommand.Execute("layout:grid");
        }
    }

    [AvaloniaFact]
    public void CloseAction_And_DeleteAll_AskFirst()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.CloseToTray.Should().BeTrue();
        ui.Vm.SettingChoiceCommand.Execute("close:exit");
        ui.Vm.CloseToTray.Should().BeFalse();
        ui.Vm.SettingChoiceCommand.Execute("close:tray");

        ui.Vm.DeleteAllDataCommand.Execute(null);
        ui.Vm.IsConfirmOpen.Should().BeTrue();
        ui.Vm.Confirm!.Details.Should().HaveCount(3, "it lists what is deleted, what is kept, and where");
        ui.Vm.CancelConfirmCommand.Execute(null);
        System.IO.File.Exists(DataWipe.MarkerPath).Should().BeFalse("cancel schedules nothing");
    }
}
