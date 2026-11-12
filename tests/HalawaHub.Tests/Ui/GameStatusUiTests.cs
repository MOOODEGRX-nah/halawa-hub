using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.Views;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using Xunit;

namespace HalawaHub.Tests.Ui;

public class GameStatusUiTests
{
    [AvaloniaTheory]
    [InlineData(Loc.Arabic, "تحديث متوفر")]
    [InlineData(Loc.English, "Update available")]
    public async Task ProbedStatus_ShowsABadgeOnTheCard(string language, string text)
    {
        using var ui = new UiHarness(language: language);
        ui.Vm.StatusProbe = new GameStatusProbe
        {
            DirExists = _ => true,
            ReadText = p => p.EndsWith("appmanifest_hades.acf") ? "\"StateFlags\" \"6\"" : null,
            SteamAppRegistry = _ => null
        };

        // "hades" ليس رقم Steam صالحًا، فنستخدم لعبة Steam برقم حقيقي الشكل
        var card = ui.Vm.Games.First(c => c.Game.Platform == "Steam");
        card.Game.Id.Should().NotBeNullOrEmpty();
        await ui.Vm.RefreshStatusesAsync();
        card.Status = GameStatus.UpdateAvailable; // الحالة نفسها، مستقلة عن رقم اللعبة في العينة
        ui.Settle();

        var badges = ui.Window.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Name == "StatusBadge" && b.IsEffectivelyVisible).ToList();
        badges.Should().ContainSingle();
        badges[0].GetVisualDescendants().OfType<TextBlock>().Single().Text.Should().Be(text);
    }

    [AvaloniaFact]
    public void DriveNotConnected_DimsTheCard()
    {
        using var ui = new UiHarness(language: Loc.English);
        var card = ui.Vm.Games.First();
        card.Status = GameStatus.DriveNotConnected;
        ui.Settle();

        card.StatusBadgeText.Should().Be("Drive not connected");
        ui.Window.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains("offline")).Should().Be(1);
    }

    [AvaloniaFact]
    public void PlayingAGameWithAnUpdate_AsksFirst_AndCancelDoesNothing()
    {
        using var ui = new UiHarness(language: Loc.English);
        var card = ui.Vm.Games.First(c => c.Game.Platform == "Steam");
        card.Status = GameStatus.UpdateAvailable;

        ui.Vm.LaunchGameCommand.Execute(card);
        ui.Settle();

        ui.Vm.IsConfirmOpen.Should().BeTrue();
        ui.Vm.Confirm!.Title.Should().Contain(card.Name);
        ui.Vm.Confirm.Choices.Select(c => c.Label).Should().Contain(new[] { "Play anyway", "Cancel" });

        ui.Vm.CancelConfirmCommand.Execute(null);
        ui.Settle();
        ui.Vm.IsConfirmOpen.Should().BeFalse();
        ui.Vm.HasActiveSession.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task ProbeFromSteamFiles_FlowsToTheCard()
    {
        var game = new GameInfo
        {
            Id = "2358720", Name = "Black Myth", Platform = "Steam",
            InstallPath = System.IO.Path.Combine("lib", "steamapps", "common", "BMW"), ExecutablePath = "steam://rungameid/2358720"
        };
        using var ui = new UiHarness(language: Loc.Arabic, games: new[] { game });
        ui.Vm.StatusProbe = new GameStatusProbe
        {
            DirExists = _ => true,
            ReadText = p => p.EndsWith("appmanifest_2358720.acf") ? "\"StateFlags\" \"38\" \"buildid\" \"18095325\" \"TargetBuildID\" \"20315824\"" : null,
            SteamAppRegistry = _ => null
        };

        await ui.Vm.RefreshStatusesAsync();
        var card = ui.Vm.Games.Single();
        card.HasUpdate.Should().BeTrue();
        card.StatusBadgeText.Should().Be("تحديث متوفر");
        card.OpenInLauncherLabel.Should().Be("حدّث في Steam");
    }
}
