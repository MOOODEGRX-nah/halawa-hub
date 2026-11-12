using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using HalawaHub.Core.Models;
using HalawaHub.Core.Store;
using HalawaHub.Tests.Ui;
using Xunit;

namespace HalawaHub.Tests;

public class CollectionRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private static GameFacts Game(string name = "Hades", string platform = "Steam", double hours = 0, int? lastPlayedDaysAgo = null,
        int addedDaysAgo = 100, bool installed = true, bool favorite = false) =>
        new(platform + "|" + name, platform, name, TimeSpan.FromHours(hours),
            lastPlayedDaysAgo == null ? null : Now.AddDays(-lastPlayedDaysAgo.Value), Now.AddDays(-addedDaysAgo), installed, favorite);

    private static CollectionRule Rule(string field, string op, string value = "") => new() { Field = field, Op = op, Value = value };

    [Fact]
    public void EachRule_MatchesOnRealData()
    {
        CollectionsService.Matches(Rule("platform", "is", "steam"), Game(), Now).Should().BeTrue();
        CollectionsService.Matches(Rule("platform", "isnot", "Steam"), Game(), Now).Should().BeFalse();
        CollectionsService.Matches(Rule("playtime", "atleast", "10"), Game(hours: 12), Now).Should().BeTrue();
        CollectionsService.Matches(Rule("playtime", "atmost", "1"), Game(hours: 12), Now).Should().BeFalse();
        CollectionsService.Matches(Rule("lastplayed", "within", "7"), Game(lastPlayedDaysAgo: 3), Now).Should().BeTrue();
        CollectionsService.Matches(Rule("lastplayed", "notwithin", "7"), Game(lastPlayedDaysAgo: 3), Now).Should().BeFalse();
        CollectionsService.Matches(Rule("lastplayed", "notwithin", "7"), Game(), Now).Should().BeTrue("never played is also not recent");
        CollectionsService.Matches(Rule("lastplayed", "never"), Game(), Now).Should().BeTrue();
        CollectionsService.Matches(Rule("added", "within", "30"), Game(addedDaysAgo: 5), Now).Should().BeTrue();
        CollectionsService.Matches(Rule("installed", "no"), Game(installed: false), Now).Should().BeTrue();
        CollectionsService.Matches(Rule("favorite", "yes"), Game(), Now).Should().BeFalse();
        CollectionsService.Matches(Rule("name", "contains", "ade"), Game(), Now).Should().BeTrue();
    }

    [Fact]
    public void AllAndAny_Combine()
    {
        var c = new GameCollection { Name = "c", IsSmart = true, Rules = { Rule("platform", "is", "Steam"), Rule("playtime", "atleast", "10") } };
        CollectionsService.Contains(c, Game(hours: 2), Now).Should().BeFalse();
        c.MatchAll = false;
        CollectionsService.Contains(c, Game(hours: 2), Now).Should().BeTrue();
    }

    [Fact]
    public void SmartCollectionWithoutRules_CannotBeSaved_AndMatchesNothing()
    {
        var empty = new GameCollection { Name = "Everything?", IsSmart = true };
        CollectionsService.CanSave(empty).Should().BeFalse();
        CollectionsService.Contains(empty, Game(), Now).Should().BeFalse();

        new GameCollection { Name = "x", IsSmart = true, Rules = { Rule("playtime", "atleast", "abc") } }
            .Invoking(c => CollectionsService.CanSave(c).Should().BeFalse()).Should().NotThrow();
        CollectionsService.CanSave(new GameCollection { Name = "x", IsSmart = true, Rules = { Rule("platform", "is", "") } }).Should().BeFalse();
        CollectionsService.CanSave(new GameCollection { Name = "  ", IsSmart = false }).Should().BeFalse("a name is required");
        CollectionsService.CanSave(new GameCollection { Name = "Manual" }).Should().BeTrue();
        CollectionsService.CanSave(new GameCollection { Name = "Smart", IsSmart = true, Rules = { Rule("lastplayed", "never") } }).Should().BeTrue();
    }

    [Fact]
    public void ManualCollection_HoldsExactlyItsGames()
    {
        var c = new GameCollection { Name = "m", Games = { "Steam|Hades" } };
        CollectionsService.Contains(c, Game(), Now).Should().BeTrue();
        CollectionsService.Contains(c, Game("Celeste"), Now).Should().BeFalse();
    }
}

public class CollectionsUiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-col-" + Guid.NewGuid().ToString("N"));

    public CollectionsUiTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private UiHarness Open(string language = Loc.English)
    {
        var ui = new UiHarness(language);
        ui.Vm.CollectionsPath = Path.Combine(_dir, "collections.json");
        ui.Vm.LoadCollections();
        return ui;
    }

    [AvaloniaFact]
    public void SmartCollection_NeedsARule_ThenFiltersTheLibrary()
    {
        using var ui = Open();
        ui.Vm.NewSmartCollectionCommand.Execute(null);
        ui.Vm.EditName = "Steam only";
        ui.Vm.CanSaveCollection.Should().BeFalse();
        ui.Vm.EditProblem.Should().Contain("at least one rule");

        ui.Vm.AddRuleCommand.Execute(null);
        ui.Vm.CanSaveCollection.Should().BeFalse("the platform isn't chosen yet");
        var rule = ui.Vm.EditRules.Single();
        rule.Platform = rule.PlatformOptions.Single(p => p.Key == "Steam");
        ui.Vm.CanSaveCollection.Should().BeTrue();
        ui.Vm.SaveCollectionCommand.Execute(null);

        var item = ui.Vm.VisibleCollections.Single();
        item.Count.Should().Be(2);
        ui.Vm.SelectCollectionCommand.Execute(item.Id);
        ui.Vm.FilteredGames.Select(g => g.Game.Platform).Distinct().Should().Equal("Steam");

        ui.Vm.SelectCollectionCommand.Execute(item.Id);
        ui.Vm.FilteredGames.Should().HaveCount(6, "clicking the chip again clears the filter");
        File.ReadAllText(ui.Vm.CollectionsPath).Should().Contain("Steam only");
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void ManualCollection_GamesAreToggledFromTheGameDialog_AndItCanBeReorderedHiddenDeleted(string language)
    {
        using var ui = Open(language);
        foreach (var name in new[] { "A", "B" })
        {
            ui.Vm.NewManualCollectionCommand.Execute(null);
            ui.Vm.EditName = name;
            ui.Vm.SaveCollectionCommand.Execute(null);
        }
        ui.Vm.Collections.Select(c => c.Name).Should().Equal("A", "B");

        var card = ui.Vm.Games.First();
        ui.Vm.OpenDetailsCommand.Execute(card);
        ui.Vm.GameMemberships.Should().HaveCount(2);
        ui.Vm.GameMemberships[0].IsMember = true;
        ui.Vm.CloseDetailsCommand.Execute(null);
        ui.Vm.Collections[0].Count.Should().Be(1);

        ui.Vm.MoveCollection(0, 1);
        ui.Vm.Collections.Select(c => c.Name).Should().Equal("B", "A");

        ui.Vm.ToggleCollectionHiddenCommand.Execute(ui.Vm.Collections[0]);
        ui.Vm.VisibleCollections.Select(c => c.Name).Should().Equal("A");

        ui.Vm.DeleteCollectionCommand.Execute(ui.Vm.Collections[1]);
        ui.Vm.IsConfirmOpen.Should().BeTrue("deleting asks first");
        ui.Vm.ChooseConfirmCommand.Execute(ui.Vm.Confirm!.Choices[0]);
        ui.Vm.Collections.Select(c => c.Name).Should().Equal("B");
    }
}

public class NotificationsAndWizardTests
{
    [AvaloniaFact]
    public void Notifications_CountUnread_RespectCategories_AndDismiss()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.Notify("scan", "one");
        ui.Vm.Notify("launch", "two");
        ui.Vm.UnreadNotifications.Should().Be(2);
        ui.Vm.Notifications.First().Text.Should().Be("two", "newest first");

        ui.Vm.ToggleNotificationsCommand.Execute(null);
        ui.Vm.IsNotificationsOpen.Should().BeTrue();
        ui.Vm.UnreadNotifications.Should().Be(0);

        ui.Vm.DismissNotificationCommand.Execute(ui.Vm.Notifications.First());
        ui.Vm.Notifications.Should().ContainSingle();
        ui.Vm.ToggleNotificationsCommand.Execute(null);

        try
        {
            ui.Vm.NotifyScan = false;
            ui.Vm.Notify("scan", "muted");
            ui.Vm.Notifications.Should().ContainSingle("a muted category adds nothing");
        }
        finally
        {
            ui.Vm.NotifyScan = true;
        }
    }

    [AvaloniaFact]
    public async Task GameUpdate_IsNotifiedOncePerGame()
    {
        var game = new GameInfo { Id = "570", Name = "Dota 2", Platform = "Steam", InstallPath = Path.Combine("lib", "steamapps", "common", "dota"), ExecutablePath = "steam://rungameid/570" };
        using var ui = new UiHarness(Loc.English, games: new[] { game });
        ui.Vm.StatusProbe = new HalawaHub.Core.Library.GameStatusProbe
        {
            DirExists = _ => true, ReadText = _ => "\"StateFlags\" \"6\"", SteamAppRegistry = _ => null
        };
        await ui.Vm.RefreshStatusesAsync();
        await ui.Vm.RefreshStatusesAsync();
        ui.Vm.Notifications.Count(n => n.Category == "update").Should().Be(1);
    }

    [Fact]
    public void Wizard_IsOnlyForBrandNewConfigFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "halawahub-wiz-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fresh = Path.Combine(dir, "new", "config.json");
            ConfigService.LoadFrom(fresh).ShowOnboarding.Should().BeTrue("first run");
            ConfigService.LoadFrom(fresh).ShowOnboarding.Should().BeTrue("still pending until finished or skipped");

            var existing = Path.Combine(dir, "config.json");
            File.WriteAllText(existing, "{\"Language\":\"ar\",\"LastSeenVersion\":\"0.0.10.65\"}");
            ConfigService.LoadFrom(existing).ShowOnboarding.Should().BeFalse("existing users never see the wizard");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void Wizard_WalksThreeSteps_AndSkipEndsIt()
    {
        using var ui = new UiHarness(Loc.English);
        var cfg = ConfigService.Load();
        cfg.ShowOnboarding = true;
        ConfigService.Save(cfg);
        // الإعدادات تُقرأ عند إنشاء الـ VM، فنبدأ المعالج يدويًا بعد ضبط الحقل في نسخته
        typeof(MainViewModel).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(ui.Vm).As<AppConfig>().ShowOnboarding = true;

        ui.Vm.StartWizardIfFirstRun();
        ui.Settle();
        ui.Vm.IsWizardOpen.Should().BeTrue();
        ui.Vm.WizardStepText.Should().Be("1 / 3");
        ui.Vm.WizardNextCommand.Execute(null);
        ui.Vm.WizardNextCommand.Execute(null);
        ui.Vm.WizardNextLabel.Should().Be("Start");
        ui.Vm.WizardBackCommand.Execute(null);
        ui.Vm.WizardStep.Should().Be(1);
        ui.Vm.WizardSkipCommand.Execute(null);

        ui.Vm.IsWizardOpen.Should().BeFalse();
        ConfigService.Load().ShowOnboarding.Should().BeFalse();
        ui.Vm.StartWizardIfFirstRun();
        ui.Vm.IsWizardOpen.Should().BeFalse("it never comes back");
    }
}

public class SubscriptionsTests
{
    [Fact]
    public void MonthlyTotal_SpreadsYearlyCosts()
    {
        var items = new[]
        {
            new Subscription { Name = "Game Pass", Cost = 12, Cycle = "monthly" },
            new Subscription { Name = "EA Play", Cost = 30, Cycle = "yearly" }
        };
        SubscriptionsService.MonthlyTotal(items).Should().Be(14.5m);
    }

    [Theory]
    [InlineData("monthly", "2026-01-31", "2026-11-11", "2026-11-30")]
    [InlineData("monthly", "2026-11-11", "2026-11-11", "2026-11-11")]
    [InlineData("yearly", "2024-03-05", "2026-11-11", "2027-03-05")]
    [InlineData("monthly", "2027-06-20", "2026-11-11", "2026-11-20")]
    public void NextRenewal_IsTheFirstOneFromToday(string cycle, string known, string today, string expected)
    {
        var s = new Subscription { Cycle = cycle, Renewal = DateTime.Parse(known) };
        SubscriptionsService.NextRenewal(s, DateTime.Parse(today)).Should().Be(DateTime.Parse(expected));
    }

    [AvaloniaFact]
    public void Form_AddsOnlyValidEntries()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.SubscriptionsPath = Path.Combine(Path.GetTempPath(), "halawahub-subs-" + Guid.NewGuid().ToString("N") + ".json");
        ui.Vm.LoadSubscriptions();
        try
        {
            ui.Vm.NewSubName = "Game Pass";
            ui.Vm.NewSubCost = "abc";
            ui.Vm.NewSubDate = "2026-12-01";
            ui.Vm.AddSubscriptionCommand.CanExecute(null).Should().BeFalse();

            ui.Vm.NewSubCost = "11.99";
            ui.Vm.AddSubscriptionCommand.CanExecute(null).Should().BeTrue();
            ui.Vm.AddSubscriptionCommand.Execute(null);

            ui.Vm.Subscriptions.Should().ContainSingle();
            ui.Vm.SubscriptionsTotalText.Should().Be("Monthly total: 11.99");
            ui.Vm.RemoveSubscriptionCommand.Execute(ui.Vm.Subscriptions[0]);
            ui.Vm.HasNoSubscriptions.Should().BeTrue();
        }
        finally
        {
            File.Delete(ui.Vm.SubscriptionsPath);
        }
    }
}

public class EpicFreeGamesTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    // نفس بنية الرد الحقيقي (نوفمبر 2026)، مختصرة
    private const string Feed = """
        {"data":{"Catalog":{"searchStore":{"elements":[
          {"title":"Free Now","productSlug":"free-now","keyImages":[{"type":"Thumbnail","url":"https://cdn/x-thumb.jpg"},{"type":"OfferImageWide","url":"https://cdn/x-wide.jpg"}],
           "catalogNs":{"mappings":[{"pageSlug":"free-now-abc","pageType":"productHome"}]},
           "promotions":{"promotionalOffers":[{"promotionalOffers":[{"startDate":"2026-11-05T16:00:00.000Z","endDate":"2026-11-12T16:00:00.000Z","discountSetting":{"discountType":"PERCENTAGE","discountPercentage":0}}]}],"upcomingPromotionalOffers":[]}},
          {"title":"Free Next Week","productSlug":"next","keyImages":[],
           "catalogNs":{"mappings":[{"pageSlug":"next","pageType":"productHome"}]},
           "promotions":{"promotionalOffers":[],"upcomingPromotionalOffers":[{"promotionalOffers":[{"startDate":"2026-11-12T16:00:00.000Z","endDate":"2026-11-19T16:00:00.000Z","discountSetting":{"discountPercentage":0}}]}]}},
          {"title":"Only On Sale","productSlug":"sale","keyImages":[],
           "catalogNs":{"mappings":[{"pageSlug":"sale","pageType":"productHome"}]},
           "promotions":{"promotionalOffers":[{"promotionalOffers":[{"startDate":"2026-11-05T16:00:00.000Z","endDate":"2026-11-12T16:00:00.000Z","discountSetting":{"discountPercentage":50}}]}]}},
          {"title":"No Promotions","productSlug":"none","keyImages":[],"promotions":null}
        ]}}}}
        """;

    [Fact]
    public void OnlyGamesFreeRightNow_AreListed()
    {
        var games = EpicFreeGames.Parse(Feed, Now);
        games.Should().ContainSingle();
        games[0].Title.Should().Be("Free Now");
        games[0].Url.Should().Be("https://store.epicgames.com/p/free-now-abc");
        games[0].ImageUrl.Should().Be("https://cdn/x-wide.jpg");
        games[0].EndsUtc.Should().Be(new DateTime(2026, 11, 12, 16, 0, 0, DateTimeKind.Utc));

        EpicFreeGames.Parse(Feed, Now.AddDays(3)).Should().BeEmpty("the offer ended");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"data\":{}}")]
    [InlineData("{\"errors\":[{\"message\":\"x\"}]}")]
    public void BadResponses_GiveAnEmptyList(string? json)
    {
        EpicFreeGames.Parse(json, Now).Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task FreeGames_LiveOnTheDealsPage_NotAboveTheLibrary()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.ClearNotificationsCommand.Execute(null);
        ui.Vm.FetchFreeGamesOverride = () => Task.FromResult<string?>(Feed.Replace("2026-11-12T16:00:00.000Z", DateTime.UtcNow.AddDays(2).ToString("o")).Replace("2026-11-05T16:00:00.000Z", DateTime.UtcNow.AddDays(-2).ToString("o")));
        await ui.Vm.LoadFreeGamesAsync();
        ui.Vm.FreeGames.Should().NotBeEmpty();
        ui.Vm.HasFreeGames.Should().BeTrue();
        Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Control>(ui.All<LibraryView>().Single(), "FreeGamesStrip").Should().BeNull("the strip took a whole band of the library");

        // إشعار واحد للمجانيات الجديدة، ولا يتكرر عند إعادة الجلب
        var note = ui.Vm.Notifications.Single(n => n.Category == "free");
        note.CanOpen.Should().BeTrue();
        await ui.Vm.LoadFreeGamesAsync();
        ui.Vm.Notifications.Count(n => n.Category == "free").Should().Be(1);

        // الضغط على الإشعار يفتح صفحة العروض
        ui.Vm.OpenNotificationCommand.Execute(note);
        ui.Settle();
        ui.Vm.IsDealsPage.Should().BeTrue();
        ui.Vm.IsNotificationsOpen.Should().BeFalse();
        ui.All<DealsView>().Should().ContainSingle();
        ui.All<LibraryView>().Should().BeEmpty();
        ui.ButtonWithText(ui.Vm.FreeGames[0].Title).Should().NotBeNull();

        ui.Vm.FetchFreeGamesOverride = () => Task.FromResult<string?>(null);
        await ui.Vm.LoadFreeGamesAsync();
        ui.Vm.HasNoDeals.Should().BeTrue("a failed fetch shows the empty text, not stale games");
        ui.Vm.GoLibraryCommand.Execute(null);
    }

    [AvaloniaFact]
    public void TopBar_HasThreePages_LibraryDealsDeveloper()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Click(ui.ButtonWithText("Deals"));
        ui.Vm.TopNavIndex.Should().Be(1);
        ui.Vm.IsDealsPage.Should().BeTrue();
        ui.Click(ui.ButtonWithText("Developer"));
        ui.Vm.TopNavIndex.Should().Be(2);
        ui.Vm.IsDeveloperPage.Should().BeTrue();
        ui.Click(ui.ButtonWithText("Library"));
        ui.Vm.IsLibraryPage.Should().BeTrue();
    }
}
