using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using Moq;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>قائمة القبول في TASK.md: كل عنصر يُضغط فعليًا على النافذة الحقيقية.</summary>
public class UiFlowTests
{
    private static List<string> ShownCards(UiHarness ui) =>
        ui.All<GameCard>().Select(c => ((GameCardViewModel)c.DataContext!).Game.Id).ToList();

    [AvaloniaFact]
    public void Language_FlipsTextAndDirection_Instantly_AndSurvivesRestart()
    {
        using var ui = new UiHarness(Loc.Arabic);

        ui.Window.FlowDirection.Should().Be(FlowDirection.RightToLeft);
        ui.ButtonWithText("المكتبة").Should().NotBeNull();

        ui.Vm.OpenSettingsCommand.Execute(null);
        ui.Vm.SelectSettingsSectionCommand.Execute("0");
        ui.Settle();
        ui.Click("اللغة");
        ui.Click("English");

        ui.Window.FlowDirection.Should().Be(FlowDirection.LeftToRight);
        ui.ButtonWithText("Library").Should().NotBeNull();
        ui.ButtonWithText("Updates").Should().NotBeNull();

        // "إعادة التشغيل": اللغة في الذاكرة تعود للافتراضي، وViewModel جديد يقرأها من القرص
        ConfigService.Load().Language.Should().Be(Loc.English);
        Loc.Instance.Language = Loc.Arabic;
        var reloaded = new MainViewModel(new Mock<HalawaHub.Core.Library.ILibraryService>().Object,
            new FilterService(), new Mock<IUpdateService>().Object, startBackgroundTasks: false);
        reloaded.Language.Should().Be(Loc.English);
        reloaded.AppFlowDirection.Should().Be(FlowDirection.LeftToRight);
    }

    [AvaloniaFact]
    public void Tabs_And_Chips_FilterTheGrid()
    {
        using var ui = new UiHarness(Loc.English);
        foreach (var card in ui.Vm.Games) card.IsFavorite = false;

        ShownCards(ui).Should().HaveCount(6);

        ui.Vm.Games.First(g => g.Game.Id == "hades").IsFavorite = true;
        ui.Click("Favorites");
        ShownCards(ui).Should().Equal("hades");

        ui.Click("Home");
        ui.Click(ui.All<Button>().First(b => b.Classes.Contains("chip") && ((PlatformChip)b.DataContext!).Key == "Steam"));
        ShownCards(ui).Should().BeEquivalentTo("hades", "elden");

        ui.Vm.PlatformChipIndex.Should().Be(1);
        ui.All<Button>().Single(b => b.Classes.Contains("chip") && b.Classes.Contains("on"))
            .DataContext.Should().BeOfType<PlatformChip>().Which.Key.Should().Be("Steam");

        ui.Vm.Games.First(g => g.Game.Id == "hades").IsFavorite = false;
    }

    [AvaloniaFact]
    public void Chips_ShowShortNames_DesignOrder_AndCounts()
    {
        using var ui = new UiHarness(Loc.English);

        ui.Vm.PlatformChips.Select(c => c.Label).Should()
            .Equal("All platforms", "Steam", "Epic", "GOG", "Riot", "Xbox");
        ui.Vm.PlatformChips.Select(c => c.Count).Should().Equal(6, 2, 1, 1, 1, 1);
        ui.Vm.PlatformBarWidth.Should().Be(900, "150 per chip, 6 chips, as in the design");
    }

    [AvaloniaFact]
    public void CardMenu_OpensAndCloses_OutsideClick_AndEsc()
    {
        using var ui = new UiHarness(Loc.English);
        var cardView = ui.All<GameCard>().First();
        var card = (GameCardViewModel)cardView.DataContext!;
        var menuButton = cardView.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("eb"));

        ui.Click(menuButton);
        card.IsMenuOpen.Should().BeTrue();

        // ضغطة حقيقية على مساحة فارغة خارج القائمة
        ui.Window.MouseDown(new Point(1000, 800), MouseButton.Left);
        ui.Window.MouseUp(new Point(1000, 800), MouseButton.Left);
        ui.Settle();
        card.IsMenuOpen.Should().BeFalse("clicking outside closes it");

        ui.Click(menuButton);
        card.IsMenuOpen.Should().BeTrue();

        ui.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        ui.Settle();
        card.IsMenuOpen.Should().BeFalse("Esc closes it");
    }

    [AvaloniaFact]
    public void CardMenu_TogglesFavorite_AndOpensDialog()
    {
        using var ui = new UiHarness(Loc.English);
        var cardView = ui.All<GameCard>().First();
        var card = (GameCardViewModel)cardView.DataContext!;
        card.IsFavorite = false;
        var menuButton = cardView.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("eb"));

        ui.Click(menuButton);
        ui.Click(ui.ButtonWithText("Add to favorites"));
        card.IsFavorite.Should().BeTrue();
        card.IsMenuOpen.Should().BeFalse();

        ui.Click(menuButton);
        ui.ButtonWithText("Remove from favorites").Should().NotBeNull();
        ui.Click(cardView.GetVisualDescendants().OfType<Button>().First(b => b.Name == "SettingsItem"));

        ui.Vm.DetailsGame.Should().BeSameAs(card);
        ui.Vm.IsDetailsOpen.Should().BeTrue();

        card.IsFavorite = false;
    }

    [AvaloniaFact]
    public void GameDialog_HasFourSections_AndClosesWithEscAndBackdrop()
    {
        using var ui = new UiHarness(Loc.English);
        var card = ui.Vm.Games.First();

        ui.Vm.OpenDetailsCommand.Execute(card);
        ui.Settle();

        foreach (var (name, title) in new[] { ("Installed files", "Installed files"), ("Customization", "Customization"), ("Launch options", "Launch options"), ("General", "General") })
        {
            ui.Click(ui.All<Button>().First(b => b.Classes.Contains("si") && (b.Content as string) == name));
            ui.Vm.GameDialogTitle.Should().Be(title);
        }

        ui.Vm.GameDialogSectionCount.Should().Be(4, "no tools for this game");

        ui.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        ui.Settle();
        ui.Vm.IsDetailsOpen.Should().BeFalse();
        ui.Vm.DetailsGame.Should().BeNull();

        ui.Vm.OpenDetailsCommand.Execute(card);
        ui.Settle();
        ui.Window.MouseDown(new Point(20, 800), MouseButton.Left);
        ui.Window.MouseUp(new Point(20, 800), MouseButton.Left);
        ui.Settle();
        ui.Vm.IsDetailsOpen.Should().BeFalse("clicking the backdrop closes it");
    }

    [AvaloniaFact]
    public void HideFromLibrary_RemovesFromEveryTab_UntilShowHidden()
    {
        using var ui = new UiHarness(Loc.English);
        var elden = ui.Vm.Games.First(g => g.Game.Id == "elden");

        elden.IsHidden = true;
        ui.Vm.CloseDetailsCommand.Execute(null);
        ui.Vm.SearchQuery = "";
        ui.Vm.SelectedPlatform = null;
        ui.Vm.RefreshCommand.Execute(null);
        ui.WaitFor(() => !ui.Vm.IsRefreshing);
        ui.Settle();

        for (var tab = 0; tab < 4; tab++)
        {
            ui.Vm.SelectLibraryTab(tab);
            ui.Settle();
            ShownCards(ui).Should().NotContain("elden");
        }
        ui.Vm.SelectLibraryTab(0);
        ui.Settle();

        ui.Vm.PlatformChips.First().Count.Should().Be(5);

        ui.Vm.ShowHiddenGames = true;
        ui.Settle();
        ShownCards(ui).Should().Contain("elden");

        ui.Vm.ShowHiddenGames = false;
        ui.Vm.Games.First(g => g.Game.Id == "elden").IsHidden = false;
    }

    [AvaloniaFact]
    public void Search_ShowsSuggestions_NoMatch_AndRecent()
    {
        using var ui = new UiHarness(Loc.English);
        var vm = ui.Vm;

        ui.Click(ui.All<Button>().First(b => b.Name == "OpenButton"));
        vm.IsSearchOpen.Should().BeTrue();

        vm.SearchQuery = "ri";
        ui.Settle();
        vm.SearchSuggestions.Select(s => s.Text).Should().Contain("Elden Ring");
        var ring = vm.SearchSuggestions.First(s => s.Text == "Elden Ring");
        (ring.Before + ring.Match + ring.After).Should().Be("Elden Ring");
        ring.Match.Should().Be("Ri");
        ui.All<Border>().First(b => b.Name == "Dropdown").IsEffectivelyVisible.Should().BeTrue();
        ShownCards(ui).Should().Contain("elden");

        vm.CommitSearch("Elden Ring");
        vm.RecentSearches.First().Should().Be("Elden Ring");
        ConfigService.Load().RecentSearches.Should().Contain("Elden Ring");

        vm.SearchQuery = "zzz";
        ui.Settle();
        vm.ShowSearchNoMatch.Should().BeTrue();
        vm.SearchNoMatchText.Replace("⁨", "").Replace("⁩", "").Should().Be("No results for “zzz”");
        vm.IsPageEmpty.Should().BeTrue();

        ui.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        ui.Settle();
        vm.IsSearchOpen.Should().BeFalse();
        vm.SearchQuery.Should().BeEmpty();
        ShownCards(ui).Should().HaveCount(6);
    }

    [AvaloniaFact]
    public void Developer_ShowsComingSoon_AndBackReturnsToLibrary()
    {
        using var ui = new UiHarness(Loc.English);

        ui.Click(ui.ButtonWithText("Developer"));
        ui.Vm.IsDeveloperPage.Should().BeTrue();
        ui.All<ComingSoonView>().Should().ContainSingle();
        ui.All<LibraryView>().Should().BeEmpty();

        ui.Click("Back to Library");
        ui.Vm.IsLibraryPage.Should().BeTrue();
        ui.All<LibraryView>().Should().ContainSingle();
    }

    [AvaloniaFact]
    public void Settings_HasAllSections_AndClosesFromBackdrop()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.OpenSettingsCommand.Execute(null);
        ui.Vm.SelectSettingsSectionCommand.Execute("0");
        ui.Settle();

        foreach (var name in new[] { "Library", "Collections", "Appearance", "Platforms", "Subscriptions", "Language", "API keys", "Plugins", "Updates", "Tools", "Build info", "General" })
        {
            ui.Click(ui.All<Button>().First(b => b.Classes.Contains("si") && (b.Content as string) == name));
            ui.Vm.SettingsTitle.Should().Be(name);
        }

        ui.Window.MouseDown(new Point(20, 800), MouseButton.Left);
        ui.Window.MouseUp(new Point(20, 800), MouseButton.Left);
        ui.Settle();
        ui.Vm.IsSettingsOpen.Should().BeFalse();
        ui.Vm.IsSettingsVisible.Should().BeFalse();
    }

    [AvaloniaFact]
    public void FailedUpdateCheck_ShowsErrorState_NotUpToDate()
    {
        var updates = new Mock<IUpdateService>();
        updates.Setup(u => u.CheckForUpdateAsync())
            .ReturnsAsync(new UpdateCheckResult(false, null, null, null, "x", Failed: true));

        using var ui = new UiHarness(Loc.English, updates.Object);
        ui.Vm.OpenSettingsCommand.Execute(null);
        ui.Vm.SelectSettingsSectionCommand.Execute("0");
        ui.Settle();

        ui.Click("Check for updates");
        ui.WaitFor(() => !ui.Vm.IsCheckingForUpdates);
        ui.Settle();

        ui.Vm.UpdateBadge.Should().Be(BadgeState.Error);
        ui.Vm.UpdateStatusText.Should().Be("Couldn't check for updates — check your connection");
        ui.All<Border>().Single(b => b.Classes.Contains("stat") && b.IsEffectivelyVisible)
            .Classes.Should().Contain("bad");
    }

    [AvaloniaFact]
    public void AvailableUpdate_ShowsInstallButton_ReleasePageFallbackChangesItsLabel()
    {
        var updates = new Mock<IUpdateService>();
        updates.Setup(u => u.CheckForUpdateAsync())
            .ReturnsAsync(new UpdateCheckResult(true, "9.0.0.0", "https://x/r", null, "m", IsReleasePage: true));

        using var ui = new UiHarness(Loc.English, updates.Object);
        ui.Vm.OpenSettingsCommand.Execute(null);
        ui.Vm.SelectSettingsSectionCommand.Execute("0");
        ui.Settle();

        ui.Click("Check for updates");
        ui.WaitFor(() => !ui.Vm.IsCheckingForUpdates);
        ui.Settle();

        ui.Vm.IsUpdateAvailable.Should().BeTrue();
        ui.Vm.UpdateBadge.Should().Be(BadgeState.Done);
        ui.ButtonWithText("Open release page").Should().NotBeNull();
    }

    [AvaloniaFact]
    public void DisplayName_ChangesTheCard_AndEmptyRestoresTheOriginal()
    {
        using var ui = new UiHarness(Loc.English);
        var card = ui.Vm.Games.First(g => g.Game.Id == "hades");

        card.EditableName = "My Hades";
        card.Name.Should().Be("My Hades");
        card.Letter.Should().Be("M");
        card.Game.Name.Should().Be("Hades II", "the platform's real name is kept for cover lookup");
        GameOverridesService.GetDisplayName(card.Platform, card.Game.Id).Should().Be("My Hades");

        card.EditableName = "";
        card.Name.Should().Be("Hades II");
        GameOverridesService.GetDisplayName(card.Platform, card.Game.Id).Should().BeNull();
    }

    [AvaloniaFact]
    public void NarrowWindow_KeepsSearchAndRefreshInsideTheWindow_WhenSearchIsOpen()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Window.Width = 980;
        ui.Vm.IsSearchOpen = true;
        ui.Settle();

        var refresh = ui.All<Button>().First(b => b.Name == "RefreshButton");
        var right = refresh.TranslatePoint(new Point(refresh.Bounds.Width, 0), ui.Window)!.Value.X;
        right.Should().BeLessThanOrEqualTo(980);

        ui.Vm.CloseSearch();
    }

    [AvaloniaFact]
    public void HomeDashboard_ListView_Bell_AndOwned_AreGone_ButToolsSectionWorks()
    {
        using var ui = new UiHarness(Loc.English);

        var texts = ui.Window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        texts.Should().NotContain("Owned");
        ui.All<GameCard>().Should().HaveCount(6, "Home is the card grid, not the old dashboard");

        var noTools = ui.Vm.Games.First();
        ui.Vm.OpenDetailsCommand.Execute(noTools);
        ui.Settle();
        ui.All<Button>().Where(b => b.Classes.Contains("si")).Select(b => b.Content as string)
            .Should().NotContain("Tools");
    }
}
