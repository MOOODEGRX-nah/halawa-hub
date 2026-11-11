using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Store;
using HalawaHub.Tests.Ui;
using Xunit;

namespace HalawaHub.Tests;

/// مدخلات السجل منسوخة من سجل ويندوز حقيقي (EA وRockstar) ومن بنية Ubisoft الموثّقة
public class RegistryLauncherProviderTests
{
    private static RegistryEntry Entry(string key, params (string Name, string Value)[] values) =>
        new(key, values.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase));

    private static Func<string, List<RegistryEntry>> Registry(Dictionary<string, List<RegistryEntry>> data) =>
        path => data.TryGetValue(path, out var list) ? list : new List<RegistryEntry>();

    [Fact]
    public void Ubisoft_ReadsInstalls_AndLaunchesByItsProtocol()
    {
        var p = new UbisoftLibraryProvider(Registry(new()
        {
            [@"SOFTWARE\Ubisoft\Launcher\Installs"] = new()
            {
                Entry("635", ("InstallDir", "D:/Ubisoft/Assassin's Creed IV Black Flag/")),
                Entry("garbage", ("InstallDir", "D:/x")),
                Entry("720")
            }
        }), _ => true);

        var game = p.ScanLibrary().Single();
        game.Id.Should().Be("635");
        game.Name.Should().Be("Assassin's Creed IV Black Flag");
        game.ExecutablePath.Should().Be("uplay://launch/635/0");
        game.Platform.Should().Be("Ubisoft");
    }

    [Fact]
    public void Ea_SkipsSteamCopies_AndUsesTheOfferId()
    {
        var p = new EaLibraryProvider(Registry(new()
        {
            [@"SOFTWARE\EA Games"] = new()
            {
                Entry("Battlefield 4", ("Install Dir", @"X:\game\Steam\steamapps\common\Battlefield 4"), ("DisplayName", "Battlefield 4™")),
                Entry("It Takes Two", ("Install Dir", @"D:\EA Games\It Takes Two\"), ("DisplayName", "It Takes Two™ (x64)")),
                Entry("Unknown Offer", ("Install Dir", @"D:\EA Games\Other"))
            },
            [@"SOFTWARE\Origin Games"] = new()
            {
                Entry("1011577", ("DisplayName", "Battlefield 4™")),
                Entry("Origin.OFR.50.0004063", ("DisplayName", "It Takes Two™"))
            }
        }), _ => true);

        var games = p.ScanLibrary().ToList();
        games.Select(g => g.Name).Should().Equal("It Takes Two", "Unknown Offer");
        games[0].ExecutablePath.Should().Be("origin2://game/launch?offerIds=Origin.OFR.50.0004063");
        games[0].InstallPath.Should().Be(@"D:\EA Games\It Takes Two");
        games[1].ExecutablePath.Should().Be("origin2://library/open", "without an offer id the EA app is opened");
    }

    [Fact]
    public void Rockstar_ListsOnlyLauncherInstalls()
    {
        var p = new RockstarLibraryProvider(Registry(new()
        {
            [@"SOFTWARE\Rockstar Games"] = new()
            {
                Entry("Launcher", ("InstallFolder", @"C:\Program Files\Rockstar Games\Launcher")),
                Entry("Rockstar Games Social Club"),
                Entry("Red Dead Redemption 2", ("InstallFolderSteam", @"X:\game\Steam\steamapps\common\Red Dead Redemption 2")),
                Entry("Grand Theft Auto V", ("InstallFolder", @"D:\Rockstar\Grand Theft Auto V"))
            }
        }), _ => true);

        var game = p.ScanLibrary().Single();
        game.Name.Should().Be("Grand Theft Auto V");
        game.ExecutablePath.Should().Be(System.IO.Path.Combine(@"C:\Program Files\Rockstar Games\Launcher", "Launcher.exe"));
        game.LaunchArguments.Should().Be("-launchTitleInFolder \"D:\\Rockstar\\Grand Theft Auto V\"");
    }

    [Fact]
    public void WithoutTheLaunchers_NothingIsFound()
    {
        var empty = Registry(new());
        new UbisoftLibraryProvider(empty, _ => true).ScanLibrary().Should().BeEmpty();
        new EaLibraryProvider(empty, _ => true).ScanLibrary().Should().BeEmpty();
        new RockstarLibraryProvider(empty, _ => true).ScanLibrary().Should().BeEmpty();
    }
}

public class SteamStoreClientTests
{
    // نفس بنية الرد الحقيقي لـ appdetails (Hades، نوفمبر 2026)، مختصرة
    private const string Details = """
        {"1145360":{"success":true,"data":{"type":"game","name":"Hades",
          "short_description":"Defy the god of the dead &amp; hack and slash out of the Underworld.",
          "pc_requirements":{"minimum":"<strong>Minimum:<\/strong><br><ul class=\"bb_ul\"><li><strong>OS *:<\/strong> Windows 7 SP1<br><\/li><li><strong>Memory:<\/strong> 4 GB RAM<br><\/li><\/ul>","recommended":"<strong>Recommended:<\/strong><br><ul class=\"bb_ul\"><li><strong>Memory:<\/strong> 8 GB RAM<\/li><\/ul>"},
          "linux_requirements":[],
          "developers":["Supergiant Games"],"publishers":["Supergiant Games","Other"],
          "header_image":"https:\/\/shared.akamai.steamstatic.com\/apps\/1145360\/header.jpg",
          "screenshots":[{"id":0,"path_thumbnail":"https:\/\/cdn\/ss_1.600x338.jpg","path_full":"https:\/\/cdn\/ss_1.1920x1080.jpg"},
                         {"id":1,"path_thumbnail":"http:\/\/insecure\/t.jpg","path_full":"http:\/\/insecure\/f.jpg"}],
          "release_date":{"coming_soon":false,"date":"17 Sep, 2020"}}}}
        """;

    [Fact]
    public void Details_AreParsed_AndHtmlBecomesText()
    {
        var d = SteamStoreClient.ParseDetails(Details, "1145360")!;
        d.Name.Should().Be("Hades");
        d.Description.Should().Be("Defy the god of the dead & hack and slash out of the Underworld.");
        d.Developers.Should().Be("Supergiant Games");
        d.Publishers.Should().Be("Supergiant Games, Other");
        d.ReleaseDate.Should().Be("17 Sep, 2020");
        d.HeaderImage.Should().StartWith("https://");
        d.WindowsRequirements.Should().Contain("OS *: Windows 7 SP1").And.Contain("Memory: 8 GB RAM").And.NotContain("<");
        d.LinuxRequirements.Should().BeEmpty("Steam sends an empty array when there are none");
        d.Screenshots.Should().ContainSingle("only https images are used");
    }

    [Theory]
    [InlineData("{\"1145360\":{\"success\":false}}")]
    [InlineData("{\"999\":{\"success\":true,\"data\":{}}}")]
    [InlineData("not json")]
    [InlineData("")]
    public void BadResponses_GiveNull(string json)
    {
        SteamStoreClient.ParseDetails(json, "1145360").Should().BeNull();
    }

    [Theory]
    [InlineData("{\"success\":1,\"results\":{\"appid\":1145360,\"resolved_category\":3,\"resolved_items\":[]}}", DeckCompatibility.Verified)]
    [InlineData("{\"success\":1,\"results\":{\"resolved_category\":2}}", DeckCompatibility.Playable)]
    [InlineData("{\"success\":1,\"results\":{\"resolved_category\":1}}", DeckCompatibility.Unsupported)]
    [InlineData("{\"success\":1,\"results\":[]}", DeckCompatibility.Unknown)]
    [InlineData("{\"success\":1,\"results\":{\"resolved_category\":9}}", DeckCompatibility.Unknown)]
    [InlineData("oops", DeckCompatibility.Unknown)]
    public void DeckCategory_IsRead(string json, DeckCompatibility expected)
    {
        SteamStoreClient.ParseDeck(json).Should().Be(expected);
    }

    [AvaloniaFact]
    public async Task DetailsSection_ShowsStoreInfo_DeckBadge_AndTheViewerCycles()
    {
        var steam = new GameInfo { Id = "1145360", Name = "Hades", Platform = "Steam", InstallPath = "/x/h", ExecutablePath = "steam://rungameid/1145360" };
        using var ui = new UiHarness(Loc.English, games: new[] { steam });
        var details = SteamStoreClient.ParseDetails(Details.Replace("http:\\/\\/insecure", "https:\\/\\/cdn2"), "1145360");
        ui.Vm.FetchStoreOverride = _ => Task.FromResult((details, DeckCompatibility.Verified));

        var card = ui.Vm.Games.Single();
        ui.Vm.OpenDetailsCommand.Execute(card);
        ui.Vm.GameDialogSectionCount.Should().Be(6, "General, Details, Files, Customization, Launch, News");
        ui.Vm.SelectGameDialogSectionCommand.Execute(MainViewModel.DetailsSection.ToString());
        await ui.Vm.LoadStoreDetailsAsync(card);
        ui.Settle();

        ui.Vm.GameDialogTitle.Should().Be("Details");
        ui.Vm.GameDialogPillIndex.Should().Be(1, "Details sits right under General");
        ui.Vm.StoreDeveloper.Should().Be("Supergiant Games");
        ui.Vm.HasDeckBadge.Should().BeTrue();
        ui.Vm.DeckText.Should().Be("Steam Deck: Verified");
        ui.Vm.StoreScreenshots.Should().HaveCount(2);

        ui.Vm.OpenMediaCommand.Execute(ui.Vm.StoreScreenshots[1]);
        ui.Vm.MediaCounter.Should().Be("2 / 2");
        ui.Vm.StepMedia(1);
        ui.Vm.MediaCounter.Should().Be("1 / 2", "it wraps around");
        ui.Vm.CloseDetailsCommand.Execute(null);
        ui.Vm.IsMediaOpen.Should().BeFalse();
    }

    [AvaloniaFact]
    public async Task Offline_ShowsAClearMessage_NotACrash()
    {
        var steam = new GameInfo { Id = "570", Name = "Dota 2", Platform = "Steam", InstallPath = "/x/d", ExecutablePath = "steam://rungameid/570" };
        using var ui = new UiHarness(Loc.English, games: new[] { steam });
        ui.Vm.FetchStoreOverride = _ => Task.FromResult(((SteamAppDetails?)null, DeckCompatibility.Unknown));
        var card = ui.Vm.Games.Single();
        ui.Vm.OpenDetailsCommand.Execute(card);
        await ui.Vm.LoadStoreDetailsAsync(card);
        ui.Vm.HasNoStoreInfo.Should().BeTrue();
        ui.Vm.HasDeckBadge.Should().BeFalse();
    }
}

public class OwnedAndMergeTests
{
    private const string Owned = """
        {"response":{"game_count":3,"games":[
          {"appid":570,"name":"Dota 2","playtime_forever":120},
          {"appid":1145360,"name":"Hades","playtime_forever":0},
          {"appid":99,"playtime_forever":0}]}}
        """;

    [Fact]
    public void OwnedGames_BecomeNotInstalledSteamGames_WithAnInstallLink()
    {
        var games = SteamOwnedGames.Parse(Owned);
        games.Select(g => g.Name).Should().Equal("Dota 2", "Hades");
        games[1].IsInstalled.Should().BeFalse();
        games[1].ExecutablePath.Should().Be("steam://install/1145360");
        SteamOwnedGames.Parse("{\"response\":{}}").Should().BeEmpty("a private profile returns an empty response");
        SteamOwnedGames.Parse("<html>Forbidden</html>").Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task WithAKey_OwnedGamesAppearAsNotInstalled_AndWithoutItTheyGo()
    {
        var installed = new GameInfo { Id = "570", Name = "Dota 2", Platform = "Steam", InstallPath = "/x/d", ExecutablePath = "steam://rungameid/570" };
        using var ui = new UiHarness(Loc.English, games: new[] { installed });
        ui.Vm.FetchOwnedOverride = (_, _) => Task.FromResult<string?>(Owned);
        var config = typeof(MainViewModel).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(ui.Vm).As<AppConfig>();
        try
        {
            config.SteamWebApiKey = "test-key";
            await ui.Vm.RefreshOwnedGamesAsync(force: true);

            ui.Vm.Games.Should().HaveCount(2, "Dota 2 is installed already, Hades is added");
            var hades = ui.Vm.Games.Single(g => g.Game.Id == "1145360");
            hades.IsInstalled.Should().BeFalse();
            hades.PlayLabel.Should().Be("Install");
            hades.StatusBadgeText.Should().Be("Not installed");
            ui.Vm.OwnedStatusText.Should().Contain("2");

            ui.Vm.SelectLibraryTab(2);
            ui.Vm.FilteredGames.Select(g => g.Name).Should().Equal("Dota 2");
            ui.Vm.SelectLibraryTab(0);

            config.SteamWebApiKey = "";
            await ui.Vm.RefreshOwnedGamesAsync(force: true);
            ui.Vm.Games.Should().ContainSingle();
        }
        finally
        {
            config.SteamWebApiKey = "";
        }
    }

    [Theory]
    [InlineData("Hades™", "hades")]
    [InlineData("Grand Theft Auto V: Enhanced", "grandtheftautovenhanced")]
    [InlineData("  ", "")]
    public void MergeKey_IgnoresSymbolsAndCase(string name, string key)
    {
        MainViewModel.MergeKey(name).Should().Be(key);
    }

    [AvaloniaFact]
    public void SameGameOnTwoPlatforms_IsOneCard_ThatAsksWhereToLaunch()
    {
        var games = new[]
        {
            new GameInfo { Id = "s1", Name = "Control", Platform = "Steam", InstallPath = "/x/s", ExecutablePath = "steam://rungameid/1" },
            new GameInfo { Id = "e1", Name = "CONTROL™", Platform = "Epic Games", InstallPath = "/x/e", ExecutablePath = "com.epicgames.launcher://apps/e1?action=launch" },
            new GameInfo { Id = "g1", Name = "Witcher 3", Platform = "GOG", InstallPath = "/x/g", ExecutablePath = "/x/g/w.exe" }
        };
        using var ui = new UiHarness(Loc.English, games: games);

        ui.Vm.FilteredGames.Should().HaveCount(2);
        var merged = ui.Vm.FilteredGames.Single(c => c.Alternates.Count == 1);
        merged.PlatformShort.Should().EndWith("+1");

        ui.Vm.LaunchGameCommand.Execute(merged);
        ui.Vm.IsConfirmOpen.Should().BeTrue();
        ui.Vm.Confirm!.Title.Should().StartWith("Launch");
        ui.Vm.Confirm.Choices.Should().HaveCount(3, "two platforms and Cancel");
        ui.Vm.CancelConfirmCommand.Execute(null);

        // اختيار منصة بعينها يعرض نسختها كما هي
        ui.Vm.SelectedPlatform = "Epic Games";
        ui.Vm.FilteredGames.Single().Alternates.Should().BeEmpty();
        ui.Vm.SelectedPlatform = null;

        try
        {
            ui.Vm.MergeDuplicates = false;
            ui.Vm.FilteredGames.Should().HaveCount(3);
        }
        finally
        {
            ui.Vm.MergeDuplicates = true;
        }
    }
}
