using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.App.Views;
using HalawaHub.Core.Covers;
using Xunit;

namespace HalawaHub.Tests.Ui;

/// <summary>تحديث التصميم، المرحلة 3: أقسام نافذة اللعبة، بطاقات الغلاف، ومتصفح SteamGridDB.</summary>
public class DesignPhase3Tests
{
    /// SteamGridDB وهمي: عدد ثابت من الصور الثابتة والمتحركة، 50 في الصفحة كالحقيقي
    private sealed class FakeSgdb : SteamGridDbClient
    {
        public int Static = 120, Animated = 7;
        public Exception? Throw;
        public readonly List<string> Calls = new();
        public FakeSgdb() : base("test-key") { }

        public override Task<SgdbGame?> FindGameAsync(string name, CancellationToken ct = default)
        {
            Calls.Add("find:" + name);
            if (Throw != null) throw Throw;
            return Task.FromResult<SgdbGame?>(name == "nothing" ? null : new SgdbGame(42, name));
        }

        public override Task<SgdbPage> GetImagesAsync(int gameId, SgdbArtType type, string? style, bool animated, int page, CancellationToken ct = default)
        {
            Calls.Add($"{type}:{style ?? "all"}:{(animated ? "anim" : "static")}:{page}");
            var total = animated ? Animated : Static;
            var items = Enumerable.Range(page * SgdbQuery.PageSize, Math.Max(0, Math.Min(SgdbQuery.PageSize, total - page * SgdbQuery.PageSize)))
                .Select(i => new SgdbImage(i, $"https://x/{i}.png", $"https://x/t{i}.png", style ?? "alternate", i % 9, animated, 600, 900))
                .ToList();
            return Task.FromResult(new SgdbPage(items, total));
        }
    }

    [Fact]
    public void Query_UsesTheDocumentedParameters()
    {
        SgdbQuery.BuildPath(42, SgdbArtType.Cover, null, false, 0)
            .Should().Be("grids/game/42?types=static&dimensions=600x900,342x482,660x930&limit=50&page=0");
        SgdbQuery.BuildPath(42, SgdbArtType.Wide, "blurred", true, 3)
            .Should().Be("grids/game/42?types=animated&styles=blurred&dimensions=460x215,920x430&limit=50&page=3");
        SgdbQuery.BuildPath(42, SgdbArtType.Background, "material", false, 1).Should().StartWith("heroes/game/42?types=static&styles=material&limit");
        SgdbQuery.BuildPath(42, SgdbArtType.Logo, "white_logo", false, 0)
            .Should().Be("logos/game/42?types=static&limit=50&page=0", "a grid style is not valid for logos and is dropped");
        SgdbQuery.BuildPath(42, SgdbArtType.Icon, "official", false, 0).Should().StartWith("icons/game/42?types=static&styles=official");
        SgdbQuery.StylesFor(SgdbArtType.Logo).Should().Equal("official", "white", "black", "custom");
    }

    [Fact]
    public void Page_ParsesScoreStyleAndTotal()
    {
        var page = SgdbQuery.ParsePage("""
            {"success":true,"page":0,"total":248,"limit":50,"data":[
              {"id":1,"score":40,"style":"white_logo","url":"https://a/1.png","thumb":"https://a/t1.png","width":600,"height":900},
              {"id":2,"score":0,"style":"material","url":"https://a/2.webp","width":600,"height":900},
              {"id":3,"style":"x"}]}
            """, animated: true);
        page.Total.Should().Be(248);
        page.Items.Should().HaveCount(2, "an item without a url is skipped");
        page.Items[0].Should().Be(new SgdbImage(1, "https://a/1.png", "https://a/t1.png", "white_logo", 40, true, 600, 900));
        page.Items[1].Thumb.Should().Be("https://a/2.webp", "no thumb falls back to the image");
    }

    [AvaloniaFact]
    public async Task Browser_ShowsStaticFirst_ThenAnimated_AndLoadsPagesWithoutALimit()
    {
        var fake = new FakeSgdb();
        var browser = new SgdbBrowser(() => fake);
        await browser.OpenAsync("Elden Ring");

        browser.Total.Should().Be(127);
        browser.Tiles.Should().HaveCount(50);
        browser.Tiles.Should().OnlyContain(t => !t.IsAnimated);
        browser.HasMore.Should().BeTrue();

        await browser.LoadMoreAsync();
        await browser.LoadMoreAsync();
        browser.Tiles.Should().HaveCount(120);
        await browser.LoadMoreAsync();
        browser.Tiles.Should().HaveCount(127);
        browser.Tiles.Skip(120).Should().OnlyContain(t => t.IsAnimated, "animated images come after the static ones, with a GIF badge");
        browser.HasMore.Should().BeFalse();
        fake.Calls.Count(c => c.Contains(":anim:")).Should().Be(1, "the first animated page was fetched once, up front");
    }

    [AvaloniaFact]
    public async Task Browser_AnimatedOnly_StyleAndType_ReloadTheSameGame()
    {
        var fake = new FakeSgdb();
        var browser = new SgdbBrowser(() => fake);
        await browser.OpenAsync("Elden Ring");

        await browser.ToggleAnimatedAsync();
        browser.Tiles.Should().HaveCount(7).And.OnlyContain(t => t.IsAnimated);

        await browser.SetTypeAsync((int)SgdbArtType.Logo);
        browser.Type.Should().Be(SgdbArtType.Logo);
        browser.StyleChips.Select(c => c.Key).Should().Equal(null, "official", "white", "black", "custom");
        await browser.SetStyleAsync("white");
        fake.Calls.Last().Should().Be("Logo:white:anim:0");
        fake.Calls.Count(c => c.StartsWith("find:")).Should().Be(1, "changing the filters keeps the matched game");
    }

    [AvaloniaFact]
    public async Task Browser_NoInternet_AndRejectedKey_ShowAClearMessage()
    {
        var fake = new FakeSgdb { Throw = new HttpRequestException("offline") };
        var browser = new SgdbBrowser(() => fake);
        await browser.OpenAsync("Elden Ring");
        browser.HasError.Should().BeTrue();
        browser.Error.Should().Be(Loc.Instance["sgNoNet"]);
        browser.HasMore.Should().BeFalse();

        fake.Throw = new HttpRequestException("401", null, HttpStatusCode.Unauthorized);
        await browser.SearchAsync();
        browser.Error.Should().Be(Loc.Instance["sgKeyBad"]);

        fake.Throw = null;
        browser.Query = "nothing";
        await browser.SearchAsync();
        browser.HasError.Should().BeFalse();
        browser.IsEmpty.Should().BeTrue();
    }

    [AvaloniaFact]
    public void Dialog_Sections_FollowTheBigPictureOrder()
    {
        using var ui = new UiHarness(Loc.English);
        var card = ui.Vm.Games.First(g => g.Name == "Cyberpunk 2077");
        ui.Vm.OpenDetailsCommand.Execute(card);
        ui.Settle();

        var dialog = ui.All<GameDialog>().Single();
        var labels = dialog.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("si") && b.IsVisible)
            .Select(b => b.Content as string).ToList();
        labels.Should().Equal("General", "Collections", "Files", "Cover", "Launch");

        ui.Vm.SelectGameDialogSectionCommand.Execute(MainViewModel.CollectionsSection.ToString());
        ui.Vm.GameDialogTitle.Should().Be("Collections");
        ui.Vm.GameDialogPillIndex.Should().Be(1);
        ui.Vm.CloseDetailsCommand.Execute(null);
    }

    [AvaloniaFact]
    public void CoverSection_TicksTheSourceInUse_AndSgdbArtIsStoredPerKind()
    {
        using var ui = new UiHarness(Loc.English);
        var card = ui.Vm.Games.First(g => g.Name == "Hades II");
        card.RestoreOriginalCover();
        card.CoverSource.Should().Be("orig");
        card.CoverSourceText.Should().Be("Original (from the store)");

        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "halawa-cover-test.png");
        System.IO.File.WriteAllBytes(file, new byte[] { 1 });
        MainViewModel.ApplySgdbArt(card, SgdbArtType.Cover, "blurred", file);
        card.IsCoverSgdb.Should().BeTrue();
        card.CoverSourceText.Should().Be("SteamGridDB · Blurred");

        card.SetCustomCover(file);
        card.IsCoverOwn.Should().BeTrue();
        card.CoverSourceText.Should().Be("Your image");

        MainViewModel.ApplySgdbArt(card, SgdbArtType.Background, "alternate", file);
        CustomArtService.Get(card.Platform, card.Game.Id, CustomArtService.Hero).Should().Be(file);
        HalawaHub.App.BigPicture.BigPictureHost.ChosenArt(card, CustomArtService.Hero).Should().Be(new Uri(file).AbsoluteUri,
            "Big Picture shows the chosen background behind the game");
        card.IsCoverOwn.Should().BeTrue("a background does not replace the cover");

        card.RestoreOriginalCover();
        card.IsCoverOriginal.Should().BeTrue();
        CustomArtService.Set(card.Platform, card.Game.Id, CustomArtService.Hero, null);
    }

    [AvaloniaFact]
    public void SearchCard_WithoutAKey_ShowsTheKeyCard()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.SteamGridDbApiKey = "";
        var card = ui.Vm.Games.First();
        ui.Vm.OpenDetailsCommand.Execute(card);
        ui.Vm.SelectGameDialogSectionCommand.Execute("2");
        ui.Vm.SearchCoversCommand.Execute(null);

        ui.Vm.IsCoverKeyOpen.Should().BeTrue();
        ui.Vm.IsCoverSearchOpen.Should().BeFalse();
        ui.Vm.IsCoverHome.Should().BeFalse();
        ui.Settle();
        var keyCard = ui.All<GameDialog>().Single().FindControl<Border>("KeyCard")!;
        keyCard.IsEffectivelyVisible.Should().BeTrue();

        ui.Vm.CloseCoverSearchCommand.Execute(null);
        ui.Vm.IsCoverHome.Should().BeTrue();
        ui.Vm.CloseDetailsCommand.Execute(null);
    }

    [Fact]
    public void Phase3_Words_MatchTheDesign()
    {
        Loc.Lookup(Loc.Arabic, "coll").Should().Be("المجموعات");
        Loc.Lookup(Loc.Arabic, "optOwn").Should().Be("غيّرها يدوياً");
        Loc.Lookup(Loc.Arabic, "optSg").Should().Be("ابحث في SteamGridDB");
        Loc.Lookup(Loc.Arabic, "optOrig").Should().Be("استرجع الغلاف الأصلي");
        Loc.Lookup(Loc.English, "sgHero").Should().Be("Backgrounds");
        Loc.Lookup(Loc.English, "sgAnim").Should().Be("Animated only");
        Loc.Lookup(Loc.Arabic, "uninstallInLauncher").Should().Be("افتح المشغّل لإلغاء التثبيت");
    }
}
