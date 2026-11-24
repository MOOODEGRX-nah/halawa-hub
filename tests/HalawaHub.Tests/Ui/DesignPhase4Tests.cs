using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using HalawaHub.Core.Store;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>تحديث التصميم، المرحلة 4: «عرض الكل» والتحديث كل ساعة وسطر آخر تحديث.</summary>
public class DesignPhase4Tests
{
    [AvaloniaFact]
    public async Task EachRow_HasShowAll_OpeningTheStoresFullPage()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.FetchDealsOverride = url => Task.FromResult<string?>(url == StoreDeals.SteamEndpoint ? StoreDealsTests.Steam : StoreDealsTests.Gog);
        await ui.Vm.LoadStoreDealsAsync();
        ui.Vm.GoDealsCommand.Execute(null);
        ui.Settle();

        var page = ui.All<DealsView>().Single();
        var more = page.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("dmore")).ToList();
        more.Select(b => b.CommandParameter).Should().Contain(new object[]
        {
            MainViewModel.EpicFreeGamesPage, MainViewModel.SteamSpecialsPage, MainViewModel.GogDealsPage
        });
        MainViewModel.EpicFreeGamesPage.Should().Be("https://store.epicgames.com/free-games");
        MainViewModel.SteamSpecialsPage.Should().Be("https://store.steampowered.com/specials");
        MainViewModel.GogDealsPage.Should().Be("https://www.gog.com/en/games?discounted=true");
        StoreDeals.MaxPerStore.Should().Be(12, "rows keep 12 per store");
        ui.Vm.GoLibraryCommand.Execute(null);
    }

    [AvaloniaFact]
    public async Task LastUpdated_CountsMinutes_InBothLanguages()
    {
        using var ui = new UiHarness(Loc.English);
        var now = DateTime.UtcNow;
        ui.Vm.DealsNow = () => now;
        ui.Vm.FetchDealsOverride = url => Task.FromResult<string?>(url == StoreDeals.SteamEndpoint ? StoreDealsTests.Steam : StoreDealsTests.Gog);
        await ui.Vm.LoadStoreDealsAsync();

        ui.Vm.HasDealsUpdated.Should().BeTrue();
        ui.Vm.DealsUpdatedText.Should().Be("Updated just now · refreshes every hour");
        ui.Vm.DealsNow = () => now.AddMinutes(5);
        ui.Vm.DealsUpdatedText.Should().Be("Updated 5 min ago · refreshes every hour");

        ui.Vm.Language = Loc.Arabic;
        ui.Vm.DealsUpdatedText.Should().Be("آخر تحديث قبل 5 دقائق · يتحدّث كل ساعة");
        ui.Vm.DealsNow = () => now.AddMinutes(2);
        ui.Vm.DealsUpdatedText.Should().Be("آخر تحديث قبل دقيقتين · يتحدّث كل ساعة");
    }

    [AvaloniaFact]
    public void Refresh_IsHourly_AndTheMinuteTicker_RunsOnlyOnTheDealsPage()
    {
        MainViewModel.DealsRefreshInterval.Should().Be(TimeSpan.FromHours(1));
        using var ui = new UiHarness(Loc.English);
        ui.Vm.IsDealsAgeTimerRunning.Should().BeFalse();
        ui.Vm.GoDealsCommand.Execute(null);
        ui.Vm.IsDealsAgeTimerRunning.Should().BeTrue();
        ui.Vm.GoLibraryCommand.Execute(null);
        ui.Vm.IsDealsAgeTimerRunning.Should().BeFalse("no ticking while the deals page is not shown");
    }

    [AvaloniaFact]
    public async Task ShowAllArrow_PointsTheReadingWay()
    {
        using var ui = new UiHarness(Loc.Arabic);
        ui.Vm.FetchDealsOverride = url => Task.FromResult<string?>(url == StoreDeals.SteamEndpoint ? StoreDealsTests.Steam : StoreDealsTests.Gog);
        await ui.Vm.LoadStoreDealsAsync();
        ui.Vm.GoDealsCommand.Execute(null);
        ui.Settle();
        var arrow = ui.All<DealsView>().Single().GetVisualDescendants().OfType<HalawaHub.App.Controls.VectorIcon>().First();
        ((Avalonia.Media.ScaleTransform)((Control)arrow.GetVisualParent()!).RenderTransform!).ScaleX.Should().Be(-1, "in Arabic the arrow points left");
        ui.Vm.Language = Loc.English;
        ui.Settle();
        ((Control)arrow.GetVisualParent()!).RenderTransform.Should().BeNull();
        ui.Vm.GoLibraryCommand.Execute(null);
    }

    [AvaloniaFact]
    public async Task AFailedHourlyRefresh_KeepsTheRows_AndAHiddenWindowStopsTheTicker()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.FetchDealsOverride = url => Task.FromResult<string?>(url == StoreDeals.SteamEndpoint ? StoreDealsTests.Steam : StoreDealsTests.Gog);
        await ui.Vm.LoadStoreDealsAsync();
        ui.Vm.SteamDeals.Should().NotBeEmpty();

        ui.Vm.FetchDealsOverride = _ => Task.FromResult<string?>(null);
        await ui.Vm.RefreshDealsAsync();
        ui.Vm.SteamDeals.Should().NotBeEmpty("no internet on the hourly refresh keeps what was shown");
        ui.Vm.OtherDeals.Should().NotBeEmpty();

        ui.Vm.GoDealsCommand.Execute(null);
        ui.Vm.IsDealsAgeTimerRunning.Should().BeTrue();
        ui.Vm.IsWindowShown = false;
        ui.Vm.IsDealsAgeTimerRunning.Should().BeFalse("no ticking while the window is in the tray or behind Big Picture");
        ui.Vm.IsWindowShown = true;
        ui.Vm.IsDealsAgeTimerRunning.Should().BeTrue();
        ui.Vm.GoLibraryCommand.Execute(null);
    }
}
