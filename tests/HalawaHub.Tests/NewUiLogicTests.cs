using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using HalawaHub.App.Controls;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Covers;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using Xunit;

namespace HalawaHub.Tests;

public class LocTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }

    [Fact]
    public void BothLanguages_HaveExactlyTheSameKeys()
    {
        Loc.ArabicStrings.Keys.Should().BeEquivalentTo(Loc.EnglishStrings.Keys);
    }

    [Fact]
    public void EveryKeyUsedInXaml_Exists()
    {
        var root = RepoFile("src", "HalawaHub.App");
        var keys = Directory.GetFiles(root, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{l:Loc (\w+)\}").Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        keys.Should().NotBeEmpty();
        keys.Should().OnlyContain(k => Loc.ArabicStrings.ContainsKey(k), "a missing key would show its raw name in the UI");
    }

    [Fact]
    public void EveryKeyUsedInCode_Exists()
    {
        var root = RepoFile("src", "HalawaHub.App");
        var keys = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?:Loc\.Instance|\bl)\[""(\w+)""\]|\.Format\(""(\w+)""")
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value))
            .Distinct()
            .ToList();

        keys.Should().NotBeEmpty();
        keys.Should().OnlyContain(k => Loc.ArabicStrings.ContainsKey(k));
    }

    [Theory]
    [InlineData("play", "تشغيل", "Play")]
    [InlineData("home", "الرئيسي", "Home")]
    [InlineData("soon", "قريباً", "Coming soon")]
    [InlineData("emptyPage", "لا يوجد شيء هنا بعد", "Nothing here yet")]
    [InlineData("saved", "تُحفظ التغييرات تلقائيًا", "Changes are saved automatically")]
    public void DesignStrings_AreVerbatim(string key, string ar, string en)
    {
        Loc.ArabicStrings[key].Should().Be(ar);
        Loc.EnglishStrings[key].Should().Be(en);
    }

    [Fact]
    public void UnknownLanguageValue_FallsBackToArabic()
    {
        var loc = Loc.Instance;
        var before = loc.Language;

        loc.Language = "fr";
        loc.Language.Should().Be(Loc.Arabic);

        loc.Language = before;
    }
}

public class ConfigCompatibilityTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "hh-cfg-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void OldConfigWithoutNewFields_StillLoads_WithDefaults_AndKeepsTheKey()
    {
        File.WriteAllText(_path, """{"SteamGridDbApiKey":"abc123","LastSeenVersion":"0.0.10.50"}""");

        var cfg = ConfigService.LoadFrom(_path);

        cfg.SteamGridDbApiKey.Should().Be("abc123");
        cfg.LastSeenVersion.Should().Be("0.0.10.50");
        cfg.Language.Should().Be("ar");
        cfg.ShowHiddenGames.Should().BeFalse();
        cfg.RecentSearches.Should().BeEmpty();
    }

    [Fact]
    public void NewFields_RoundTrip()
    {
        ConfigService.SaveTo(_path, new AppConfig { Language = "en", ShowHiddenGames = true, RecentSearches = { "x" } });

        var cfg = ConfigService.LoadFrom(_path);

        cfg.Language.Should().Be("en");
        cfg.ShowHiddenGames.Should().BeTrue();
        cfg.RecentSearches.Should().Equal("x");
    }
}

public class GameCardLogicTests
{
    private static GameCardViewModel Card(string id, string platform = "Steam", string? cover = null) => new(
        new GameInfo { Id = id, Name = "Game " + id, Platform = platform, CoverImageUrl = cover },
        Array.Empty<IGameTool>());

    [Theory]
    [InlineData("Epic Games", "Epic")]
    [InlineData("Riot Games", "Riot")]
    [InlineData("Xbox / Microsoft Store", "Xbox")]
    [InlineData("Steam", "Steam")]
    [InlineData("MyPlugin", "MyPlugin")]
    public void PlatformBadge_UsesTheShortName(string platform, string expected)
    {
        Card("a", platform).PlatformShort.Should().Be(expected);
    }

    [Fact]
    public void RestoreOriginal_RemovesTheCustomCover_AndBringsThePlatformCoverBack()
    {
        var card = Card("restore-" + Guid.NewGuid().ToString("N"), cover: "https://x/original.jpg");
        var local = Path.Combine(Path.GetTempPath(), "cover.png");

        card.SetCustomCover(local);
        card.HasCustomCover.Should().BeTrue();
        card.CoverImageUrl.Should().StartWith("file:");
        CustomCoverService.Get(card.Platform, card.Game.Id).Should().Be(local);

        card.RestoreOriginalCover();

        card.HasCustomCover.Should().BeFalse();
        card.CoverImageUrl.Should().Be("https://x/original.jpg");
        CustomCoverService.Get(card.Platform, card.Game.Id).Should().BeNull();
    }

    [Fact]
    public void Hidden_PersistsAcrossCardInstances()
    {
        var id = "hidden-" + Guid.NewGuid().ToString("N");

        Card(id).IsHidden = true;
        Card(id).IsHidden.Should().BeTrue();

        Card(id).IsHidden = false;
        Card(id).IsHidden.Should().BeFalse();
    }

    [Fact]
    public void PaletteIndex_IsStableAndInRange()
    {
        var a = Card("stable");
        a.PaletteIndex.Should().Be(Card("stable").PaletteIndex);
        a.PaletteIndex.Should().BeInRange(0, 5);
    }

    [Fact]
    public void PickedCoverPath_StaysInTheDataFolder_WithASafeExtension()
    {
        var card = Card("x");

        MainViewModel.BuildPickedCoverPath(card, "https://cdn/a/b.PNG").Should().EndWith(".png");
        MainViewModel.BuildPickedCoverPath(card, "https://cdn/a/b.exe?x=1").Should().EndWith(".png");
        MainViewModel.BuildPickedCoverPath(card, "https://cdn/a/b.webp")
            .Should().StartWith(HalawaHub.Core.AppPaths.DataDirectory);
    }

    [Fact]
    public void Filter_ExcludesHidden_ByDefault_AndFiltersByPlatform()
    {
        var visible = Card("v1");
        var other = Card("v2", "GOG");
        var hidden = Card("h-" + Guid.NewGuid().ToString("N"));
        hidden.IsHidden = true;

        var service = new FilterService();
        var games = new[] { visible, other, hidden };
        IEnumerable<GameCardViewModel> Run(string? platform = null, bool includeHidden = false) =>
            service.ApplyFilter(games, "", "الكل", "name", "الكل", "المفضلة", "المثبتة", "المضافة حديثًا", platform, includeHidden);

        Run().Should().BeEquivalentTo(new[] { visible, other });
        Run(includeHidden: true).Should().HaveCount(3);
        Run(platform: "GOG").Should().Equal(other);

        hidden.IsHidden = false;
    }
}

public class GameSizeTests
{
    [Theory]
    [InlineData(null, "—")]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2.0 KB")]
    [InlineData(73_443_735_552L, "68.4 GB")]
    public void Format_MatchesTheDesign(long? bytes, string expected)
    {
        GameSizeService.Format(bytes).Should().Be(expected);
    }

    [Fact]
    public async Task Measures_Recursively_InTheBackground_AndCaches()
    {
        var dir = Directory.CreateTempSubdirectory("hh-size-").FullName;
        Directory.CreateDirectory(Path.Combine(dir, "sub"));
        await File.WriteAllBytesAsync(Path.Combine(dir, "a.bin"), new byte[100]);
        await File.WriteAllBytesAsync(Path.Combine(dir, "sub", "b.bin"), new byte[50]);

        (await GameSizeService.GetSizeAsync(dir)).Should().Be(150);

        await File.WriteAllBytesAsync(Path.Combine(dir, "c.bin"), new byte[1000]);
        (await GameSizeService.GetSizeAsync(dir)).Should().Be(150, "the result is cached for the session");

        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task MissingOrEmptyPath_IsUnknown()
    {
        (await GameSizeService.GetSizeAsync("")).Should().BeNull();
        (await GameSizeService.GetSizeAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))).Should().BeNull();
    }
}

public class CoverCandidatesTests
{
    [Fact]
    public void Parse_ReadsUrlAndThumb_RespectsTheLimit_AndSkipsBadEntries()
    {
        const string json = """
        {"data":[
          {"url":"https://x/1.png","thumb":"https://x/1t.png"},
          {"thumb":"https://x/no-url.png"},
          {"url":"https://x/2.png"},
          {"url":"https://x/3.png","thumb":"https://x/3t.png"}
        ]}
        """;

        var list = SteamGridDbClient.ParseCandidates(json, max: 2);

        list.Should().HaveCount(2);
        list[0].Should().Be(new CoverCandidate("https://x/1.png", "https://x/1t.png"));
        list[1].ThumbUrl.Should().Be("https://x/2.png", "falls back to the full image");
    }

    [Fact]
    public void Parse_NoData_IsEmpty()
    {
        SteamGridDbClient.ParseCandidates("""{"success":false}""", 5).Should().BeEmpty();
    }
}

public class AutoFillGridTests
{
    [Theory]
    [InlineData(1224, 6)] // نافذة 1280 كما في التصميم
    [InlineData(170, 1)]
    [InlineData(360, 2)]
    [InlineData(100, 1)]
    public void Columns_FollowAutoFillMinmax170Gap20(double width, int expected)
    {
        AutoFillGrid.ColumnsFor(width).Should().Be(expected);
    }
}
