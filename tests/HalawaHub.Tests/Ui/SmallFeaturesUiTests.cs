using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using HalawaHub.Core.Models;
using HalawaHub.Core.News;
using Xunit;

namespace HalawaHub.Tests.Ui;

public class SmallFeaturesUiTests
{
    [AvaloniaFact]
    public void Sort_ByPlayTime_AndReverse()
    {
        var games = new[]
        {
            new GameInfo { Id = "pt-a", Name = "A", Platform = "GOG", InstallPath = "/x/a" },
            new GameInfo { Id = "pt-b", Name = "B", Platform = "GOG", InstallPath = "/x/b" },
            new GameInfo { Id = "pt-c", Name = "C", Platform = "GOG", InstallPath = "/x/c" }
        };
        PlayTimeService.AddSeconds("GOG", "pt-b", 7200);
        PlayTimeService.AddSeconds("GOG", "pt-c", 600);
        using var ui = new UiHarness(games: games);

        ui.Vm.SetSortMode("playtime");
        ui.Vm.FilteredGames.Select(c => c.Name).Should().Equal("B", "C", "A");

        ui.Vm.SortReverse = true;
        ui.Vm.FilteredGames.Select(c => c.Name).Should().Equal("A", "C", "B");

        ui.Vm.SortReverse = false;
        ui.Vm.SetSortMode("name");
        ui.Vm.FilteredGames.Select(c => c.Name).Should().Equal("A", "B", "C");
    }

    [AvaloniaTheory]
    [InlineData(Loc.Arabic)]
    [InlineData(Loc.English)]
    public void CtrlK_OpensQuickSearch_ArrowsAndEnterOpenTheGame(string language)
    {
        using var ui = new UiHarness(language: language);
        ui.Window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Control);
        ui.Settle();
        ui.Vm.IsQuickSearchOpen.Should().BeTrue();
        ui.All<QuickSearchDialog>().Should().ContainSingle();

        ui.Vm.QuickSearchQuery = "e";
        ui.Vm.QuickSearchResults.Should().NotBeEmpty();
        ui.Vm.QuickSearchResults.All(c => c.Name.Contains("e", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        // ما يبدأ بالنص قبل ما يحتويه
        ui.Vm.QuickSearchResults.First().Name.Should().StartWith("E");

        ui.Vm.MoveQuickSearch(1);
        var expected = ui.Vm.QuickSearchResults[ui.Vm.QuickSearchIndex];
        ui.Vm.ConfirmQuickSearch();
        ui.Settle();

        ui.Vm.IsQuickSearchOpen.Should().BeFalse();
        ui.Vm.DetailsGame.Should().BeSameAs(expected);
    }

    [AvaloniaFact]
    public void Escape_ClosesQuickSearchFirst()
    {
        using var ui = new UiHarness();
        ui.Vm.OpenQuickSearch();
        ui.Settle();
        ui.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        ui.Settle();
        ui.Vm.IsQuickSearchOpen.Should().BeFalse();
    }

    [AvaloniaFact]
    public void EmptyLibrary_ShowsScanThisPc()
    {
        using var ui = new UiHarness(language: Loc.English, games: Array.Empty<GameInfo>());
        ui.Vm.IsLibraryEmpty.Should().BeTrue();
        ui.ButtonWithText("Scan this PC").Should().NotBeNull();
    }

    [AvaloniaFact]
    public void PlayTimeOnCards_CanBeHidden()
    {
        var game = new GameInfo { Id = "stats-1", Name = "Stats", Platform = "GOG", InstallPath = "/x/s" };
        PlayTimeService.AddSeconds("GOG", "stats-1", 3 * 3600);
        using var ui = new UiHarness(language: Loc.English, games: new[] { game });
        var card = ui.Vm.Games.Single();

        card.HasStatsLine.Should().BeTrue();
        card.StatsText.Should().Contain("3");

        ui.Vm.ShowPlayTimeOnCards = false;
        card.HasStatsLine.Should().BeFalse();
        ui.Vm.ShowPlayTimeOnCards = true;
    }

    [AvaloniaFact]
    public async Task NewsTab_ShowsSteamNews_AndOnlyForSteam()
    {
        var steam = new GameInfo { Id = "570", Name = "Dota 2", Platform = "Steam", InstallPath = "/x/d", ExecutablePath = "steam://rungameid/570" };
        var gog = new GameInfo { Id = "gog-1", Name = "Witcher", Platform = "GOG", InstallPath = "/x/w" };
        using var ui = new UiHarness(language: Loc.English, games: new[] { steam, gog });
        ui.Vm.FetchNewsOverride = (_, id) => Task.FromResult(new List<NewsItem>
        {
            new("Dota 2", "Patch 7.40", "https://store.steampowered.com/news/1", new DateTime(2026, 11, 9))
        });

        var dota = ui.Vm.Games.Single(c => c.Game.Id == "570");
        ui.Vm.OpenDetailsCommand.Execute(dota);
        ui.Vm.GameDialogSectionCount.Should().Be(6);
        ui.Vm.SelectGameDialogSectionCommand.Execute("4");
        await ui.Vm.LoadGameNewsAsync(dota);
        ui.Settle();

        ui.Vm.GameDialogTitle.Should().Be("News");
        ui.Vm.GameNews.Select(n => n.Title).Should().Contain("Patch 7.40");

        ui.Vm.CloseDetailsCommand.Execute(null);
        ui.Settle();
        var witcher = ui.Vm.Games.Single(c => c.Game.Id == "gog-1");
        witcher.HasNews.Should().BeFalse();
        ui.Vm.OpenDetailsCommand.Execute(witcher);
        ui.Vm.GameDialogSectionCount.Should().Be(4);
    }
}
