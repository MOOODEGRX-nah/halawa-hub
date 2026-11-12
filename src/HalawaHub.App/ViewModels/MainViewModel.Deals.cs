using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using HalawaHub.Core;
using HalawaHub.Core.Store;

namespace HalawaHub.App.ViewModels;

public sealed class DealItem
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? ImageUrl { get; init; }
    public required string DiscountText { get; init; }
    public required string PriceText { get; init; }
    public required string OldPriceText { get; init; }
    public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
    public bool HasOldPrice => !string.IsNullOrEmpty(OldPriceText) && OldPriceText != PriceText;
}

/// صفحة العروض: المجانيات في الصف الأول، ثم تخفيضات Steam، ثم باقي المتاجر (GOG الآن)
public partial class MainViewModel
{
    public ObservableCollection<DealItem> SteamDeals { get; } = new();
    public ObservableCollection<DealItem> OtherDeals { get; } = new();

    public bool HasSteamDeals => SteamDeals.Count > 0;
    public bool HasOtherDeals => OtherDeals.Count > 0;

    /// للاختبارات: بدل الشبكة (المعامل عنوان المصدر)
    internal Func<string, Task<string?>>? FetchDealsOverride { get; set; }

    internal async Task LoadStoreDealsAsync()
    {
        if (!_config.ShowFreeGames)
        {
            SteamDeals.Clear();
            OtherDeals.Clear();
            RaiseDeals();
            return;
        }

        var steam = StoreDeals.ParseSteam(await DealsJsonAsync(StoreDeals.SteamEndpoint, "steam-deals.json", j => StoreDeals.ParseSteam(j).Count > 0));
        var gog = StoreDeals.ParseGog(await DealsJsonAsync(StoreDeals.GogEndpoint, "gog-deals.json", j => StoreDeals.ParseGog(j).Count > 0));
        Fill(SteamDeals, steam);
        Fill(OtherDeals, gog);
        RaiseDeals();
    }

    private static void Fill(ObservableCollection<DealItem> target, List<StoreDeal> deals)
    {
        target.Clear();
        foreach (var d in deals)
            target.Add(new DealItem
            {
                Title = d.Title, Url = d.Url, ImageUrl = d.ImageUrl,
                DiscountText = "-" + d.DiscountPercent + "%", PriceText = d.PriceText, OldPriceText = d.OldPriceText
            });
    }

    /// من الملف المخزَّن إن كان حديثًا، وإلا من الشبكة. لا يُخزَّن إلا رد فيه عروض فعلًا.
    private async Task<string?> DealsJsonAsync(string endpoint, string cacheFile, Func<string, bool> isUseful)
    {
        try
        {
            if (FetchDealsOverride != null) return await FetchDealsOverride(endpoint);

            var cachePath = AppPaths.InData("cache", cacheFile);
            if (File.Exists(cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < FreeGamesMaxAge)
                return await File.ReadAllTextAsync(cachePath);

            var json = await StoreDeals.FetchJsonAsync(endpoint);
            if (json != null && isUseful(json))
            {
                try { AtomicFile.WriteAllText(cachePath, json); } catch (Exception ex) { Log.Error("deals cache save failed", ex); }
            }
            return json;
        }
        catch (Exception ex)
        {
            Log.Error("deals load failed: " + cacheFile, ex);
            return null;
        }
    }
}
