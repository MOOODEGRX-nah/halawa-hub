using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.Views;
using HalawaHub.Core.Store;
using HalawaHub.Tests.Ui;
using Xunit;

namespace HalawaHub.Tests;

/// الردود مقصوصة من ردود حقيقية للمتجرين (نوفمبر 2026)
public class StoreDealsTests
{
    internal const string Steam = """
    {"0":{"id":"cat_spotlight","name":"Spotlights","items":[{"name":"MIDWEEK DEAL"}]},
     "specials":{"id":"cat_specials","name":"Specials","items":[
      {"id":489830,"type":0,"name":"The Elder Scrolls V: Skyrim Special Edition","discounted":true,"discount_percent":80,"original_price":15900,"final_price":3180,"currency":"SAR","large_capsule_image":"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/489830/capsule_616x353.jpg","header_image":"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/489830/header.jpg"},
      {"id":489830,"type":0,"name":"Duplicate","discounted":true,"discount_percent":80,"original_price":15900,"final_price":3180,"currency":"SAR"},
      {"id":817628,"type":1,"name":"Some Package","discounted":true,"discount_percent":35,"original_price":2000,"final_price":1300,"currency":"USD","header_image":"http://insecure.example/x.jpg"},
      {"id":5,"type":0,"name":"Not discounted","discounted":false,"discount_percent":0,"original_price":1000,"final_price":1000,"currency":"USD"},
      {"id":"bad","name":"Broken id","discount_percent":50}
     ]}}
    """;

    internal const string Gog = """
    {"pages":66,"productCount":3298,"products":[
      {"id":"2068449424","slug":"warhammer_survivors","title":"Warhammer Survivors","coverHorizontal":"https://images.gog-statics.com/40ff.png",
       "price":{"final":"$7.19","base":"$7.99","discount":"-10%","finalMoney":{"amount":"7.19","currency":"USD"}},"storeLink":"https://www.gog.com/en/game/warhammer_survivors"},
      {"id":"2","title":"Elsewhere","price":{"final":"$1","base":"$2","discount":"-50%"},"storeLink":"https://evil.example/game"},
      {"id":"3","title":"No discount","price":{"final":"$2","base":"$2","discount":null},"storeLink":"https://www.gog.com/en/game/x"},
      {"id":"4","title":"No price","storeLink":"https://www.gog.com/en/game/y"}
    ]}
    """;

    [Fact]
    public void Steam_ReadsSpecials_WithPricesAndTheRightStoreLink()
    {
        var deals = StoreDeals.ParseSteam(Steam);
        deals.Should().HaveCount(2, "the duplicate, the undiscounted one and the broken one are dropped");
        deals[0].Should().Be(new StoreDeal("Steam", "The Elder Scrolls V: Skyrim Special Edition", "https://store.steampowered.com/app/489830",
            "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/489830/header.jpg", 80, "31.80 SAR", "159.00 SAR"));
        deals[1].Url.Should().Be("https://store.steampowered.com/sub/817628", "type 1 is a package");
        deals[1].ImageUrl.Should().BeNull("only https images are loaded");
    }

    [Fact]
    public void Gog_ReadsDiscountedProducts_AndKeepsOnlyGogLinks()
    {
        var deal = StoreDeals.ParseGog(Gog).Single();
        deal.Should().Be(new StoreDeal("GOG", "Warhammer Survivors", "https://www.gog.com/en/game/warhammer_survivors",
            "https://images.gog-statics.com/40ff.png", 10, "$7.19", "$7.99"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<html>blocked</html>")]
    [InlineData("{\"specials\":5,\"products\":\"x\"}")]
    public void AChangedOrBrokenAnswer_GivesNoDeals_NotACrash(string? json)
    {
        StoreDeals.ParseSteam(json).Should().BeEmpty();
        StoreDeals.ParseGog(json).Should().BeEmpty();
    }

    [AvaloniaFact]
    public async Task DealsPage_ShowsSteamThenOtherStores_AndHidesASectionThatFailed()
    {
        using var ui = new UiHarness(Loc.English);
        ui.Vm.FetchDealsOverride = url => Task.FromResult<string?>(url == StoreDeals.SteamEndpoint ? Steam : Gog);
        await ui.Vm.LoadStoreDealsAsync();
        ui.Vm.GoDealsCommand.Execute(null);
        ui.Settle();

        ui.Vm.SteamDeals.Select(d => d.DiscountText).Should().Equal("-80%", "-35%");
        ui.Vm.OtherDeals.Single().PriceText.Should().Be("$7.19");
        ui.Vm.HasNoDeals.Should().BeFalse();
        ui.ButtonWithText("Warhammer Survivors").Should().NotBeNull();

        // الصف الأول للمجانيات، ثم Steam، ثم باقي المتاجر
        var page = ui.All<DealsView>().Single();
        var names = new[] { "FreeGamesSection", "SteamDealsSection", "OtherDealsSection" };
        var order = names.Select(n => Avalonia.Controls.ControlExtensions.FindControl<Avalonia.Controls.Control>(page, n)!)
            .Select(c => ((Avalonia.Controls.Panel)c.Parent!).Children.IndexOf(c)).ToList();
        order.Should().BeInAscendingOrder();

        ui.Vm.FetchDealsOverride = url => Task.FromResult<string?>(url == StoreDeals.SteamEndpoint ? null : Gog);
        await ui.Vm.LoadStoreDealsAsync();
        ui.Vm.HasSteamDeals.Should().BeFalse();
        ui.Vm.HasOtherDeals.Should().BeTrue();
        ui.Vm.GoLibraryCommand.Execute(null);
    }
}
