using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.BigPicture.Input;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>تحديث التصميم 5.1: تبويب العروض في الشاشة الكبيرة.</summary>
public class BigPictureDealsTests : IDisposable
{
    public void Dispose() => Loc.Instance.Language = Loc.Arabic;

    private static BigPictureViewModel Vm(FakeBpHost host) => new(host, _ => System.Threading.Tasks.Task.CompletedTask);

    private static FakeBpHost WithDeals()
    {
        var host = FakeBpHost.WithGames(3);
        host.DealList.Add(new BpDealSource("freeThisWeek",
            new[] { new BpDealData("A", "Free until x", "Free", null, "https://e/a", true), new BpDealData("B", "Free until y", "Free", null, "https://e/b", true) },
            "https://store.epicgames.com/free-games"));
        host.DealList.Add(new BpDealSource("dealsSteam",
            Enumerable.Range(0, 12).Select(i => new BpDealData("S" + i, "$1", "-50%", null, "https://s/" + i, false)).ToList(),
            "https://store.steampowered.com/specials"));
        host.DealList.Add(new BpDealSource("dealsOther", new List<BpDealData>(), "https://www.gog.com/en/games?discounted=true"));
        host.DealsUpdatedText = "Updated 3 min ago · refreshes every hour";
        return host;
    }

    [AvaloniaFact]
    public void Deals_is_the_third_of_four_tabs()
    {
        BigPictureViewModel.Tabs.Should().Equal(BpScreen.Home, BpScreen.Library, BpScreen.Deals, BpScreen.Search);
    }

    [AvaloniaFact]
    public void Each_row_ends_with_show_all_and_empty_rows_are_left_out()
    {
        var host = WithDeals();
        var vm = Vm(host);
        vm.GoTab(BpScreen.Deals);

        vm.DealRows.Should().HaveCount(2, "a store without deals gets no row");
        vm.DealRows[0].Items.Should().HaveCount(3);
        vm.DealRows[0].Items.Last().IsMore.Should().BeTrue();
        vm.DealRows[0].Items.Last().Url.Should().Be("https://store.epicgames.com/free-games");
        vm.DealRows[1].Items.Should().HaveCount(13, "12 Steam deals and Show all");
        vm.FocusKey.Should().Be("deal.0.0");
        vm.HasDealsUpdated.Should().BeTrue();
    }

    [AvaloniaFact]
    public void A_opens_in_the_browser_with_a_toast_and_the_row_slides_to_keep_focus_visible()
    {
        Loc.Instance.Language = Loc.English;
        var host = WithDeals();
        var vm = Vm(host);
        string? toast = null;
        vm.Toast += t => toast = t;
        vm.GoTab(BpScreen.Deals);

        vm.Press(BpButton.Down);
        for (var i = 0; i < 12; i++) vm.Press(BpButton.Right);
        vm.FocusKey.Should().Be("deal.1.12");
        vm.DealRows[1].Offset.Should().Be(8, "the focused card stays visible");
        vm.DealsShiftY.Should().Be(0);

        vm.Press(BpButton.A);
        host.Calls.Should().Contain("url:https://store.steampowered.com/specials");
        toast.Should().Be("Opens in your browser");

        vm.Press(BpButton.Up);
        vm.FocusKey.Should().Be("deal.0.2", "moving up keeps the column when it exists");
        vm.DealRows[1].Offset.Should().Be(0, "a row the focus left goes back to its start");

        vm.Press(BpButton.B);
        vm.Screen.Should().Be(BpScreen.Home);
    }
}
