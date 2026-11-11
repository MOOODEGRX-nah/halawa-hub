using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using HalawaHub.Tests.Ui;
using Xunit;

namespace HalawaHub.Tests;

/// اختبارات إصلاحات المراجعة الشاملة
public class ReviewFixesTests
{
    [Fact]
    public void DataWipe_RefusesDriveRootAndUserFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DataWipe.IsUnsafeFolder(home).Should().BeTrue();
        DataWipe.IsUnsafeFolder(home + Path.DirectorySeparatorChar).Should().BeTrue();
        DataWipe.IsUnsafeFolder(Path.GetPathRoot(Path.GetFullPath(home))!).Should().BeTrue();
        DataWipe.IsUnsafeFolder(Path.Combine(home, "some", "Halawa-Hub")).Should().BeFalse();
    }

    [Fact]
    public void DataWipe_StillDeletesANormalFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halawa-wipe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "covers"));
        File.WriteAllText(Path.Combine(dir, "favorites.json"), "[]");
        File.WriteAllText(Path.Combine(dir, DataWipe.MarkerName), "x");
        try
        {
            DataWipe.RunIfScheduled(dir).Should().BeTrue();
            Directory.EnumerateFileSystemEntries(dir).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ManualGame_UnreadableFile_IsRejected_NotThrown()
    {
        var file = Path.Combine(Path.GetTempPath(), "halawa-" + Guid.NewGuid().ToString("N") + ".lnk");
        File.WriteAllText(file, "x");
        try
        {
            ManualGameRecord? record = null;
            var act = () => record = ManualGames.FromFile(file, _ => throw new UnauthorizedAccessException("locked"));
            act.Should().NotThrow();
            record.Should().BeNull();
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Uninstall_IsNotOfferedForAGameThatIsNotInstalled()
    {
        var game = new GameInfo { Id = "730", Name = "Owned only", Platform = "Steam", IsInstalled = false };
        new GameCardViewModel(game, Array.Empty<IGameTool>()).CanUninstall.Should().BeFalse();
    }

    [Fact]
    public async Task SafeUi_SwallowsAnExceptionFromAClickHandler()
    {
        var ran = false;
        SafeUi.Run(async () =>
        {
            await Task.Yield();
            ran = true;
            throw new InvalidOperationException("clipboard busy");
        }, "test");
        for (var i = 0; i < 100 && !ran; i++) await Task.Delay(10);
        ran.Should().BeTrue();
    }

    [AvaloniaFact]
    public void Notifications_FollowTheLanguage()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.ClearNotificationsCommand.Execute(null);
        ui.Vm.NotifyKey("scan", "ntScan", 21, 2);
        ui.Vm.Notifications[0].Text.Should().Be("Scan finished: 21 games, 2 new.");

        ui.Vm.Language = Loc.Arabic;
        ui.Vm.Notifications[0].Text.Should().StartWith("انتهى الفحص");
    }

    [AvaloniaFact]
    public void CardMenu_IsBuiltOnlyWhenFirstOpened()
    {
        using var ui = new UiHarness(Loc.English);
        var cardView = ui.All<GameCard>().First();
        var card = (GameCardViewModel)cardView.DataContext!;
        var menu = cardView.FindControl<Border>("Menu")!;

        card.MenuContent.Should().BeNull();
        menu.GetVisualDescendants().OfType<Button>().Should().BeEmpty("the four menu buttons are not built for every card");

        ui.Vm.ToggleCardMenu(card);
        ui.Settle();
        menu.GetVisualDescendants().OfType<Button>().Should().HaveCount(4);

        ui.Vm.ToggleCardMenu(card);
        ui.Settle();
        card.MenuContent.Should().NotBeNull("built once, kept afterwards");
    }

    [AvaloniaFact]
    public void OnlyTheVisibleLibraryLayoutGetsTheGames()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.SetLibraryLayout("grid");
        ui.Vm.GridGames.Should().NotBeNull();
        ui.Vm.ListGames.Should().BeNull();

        ui.Vm.SetLibraryLayout("list");
        ui.Settle();
        ui.Vm.GridGames.Should().BeNull();
        ui.Vm.ListGames.Should().NotBeNull();
        ui.All<GameCard>().Should().BeEmpty("the hidden grid builds no cards");
        ui.Vm.SetLibraryLayout("grid");
    }

    [AvaloniaFact]
    public void SearchHalo_DoesNotPulseForever()
    {
        // بلا حركة: لا نبض إطلاقًا
        using (var still = new UiHarness(Loc.English))
            still.All<SearchBox>().First().IsHaloPulsing.Should().BeFalse();

        using var ui = new UiHarness(Loc.English, motion: true);
        var box = ui.All<SearchBox>().First();
        box.PulseHalo();
        box.IsHaloPulsing.Should().BeTrue();
        SearchBox.HaloPulseTime.Should().BeLessThan(TimeSpan.FromSeconds(15), "an endless animation keeps the render loop busy while idle");

        // علامة "?" لا تتحرك وهي مخفية
        var bubble = box.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "?");
        ui.Vm.ShowSearchNoMatch.Should().BeFalse();
        bubble.Classes.Contains("bob").Should().BeFalse();
    }
}
