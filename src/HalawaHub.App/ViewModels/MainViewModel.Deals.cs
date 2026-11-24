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
            if (FetchDealsOverride != null)
            {
                var fake = await FetchDealsOverride(endpoint);
                if (fake != null) MarkDealsUpdated(DealsNow());
                return fake;
            }

            var cachePath = AppPaths.InData("cache", cacheFile);
            if (File.Exists(cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < FreeGamesMaxAge)
            {
                MarkDealsUpdated(File.GetLastWriteTimeUtc(cachePath));
                return await File.ReadAllTextAsync(cachePath);
            }

            var json = await StoreDeals.FetchJsonAsync(endpoint);
            if (json != null) MarkDealsUpdated(DateTime.UtcNow);
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

    // ===== «عرض الكل» والتحديث كل ساعة =====

    /// صفحات المتاجر الكاملة («عرض الكل» في آخر كل صف)
    public const string EpicFreeGamesPage = "https://store.epicgames.com/free-games";
    public const string SteamSpecialsPage = "https://store.steampowered.com/specials";
    public const string GogDealsPage = "https://www.gog.com/en/games?discounted=true";

    /// مؤقت واحد كل ساعة، ولا شيء غيره: لا استطلاع أثناء التصغير غير هذا
    public static readonly TimeSpan DealsRefreshInterval = TimeSpan.FromHours(1);

    private Avalonia.Threading.DispatcherTimer? _dealsTimer;
    /// يحدّث «قبل N دقيقة» كل دقيقة، ويعمل فقط وصفحة العروض ظاهرة
    private Avalonia.Threading.DispatcherTimer? _dealsAgeTimer;
    private DateTime? _dealsUpdatedUtc;

    /// للاختبارات: الوقت الحالي
    internal Func<DateTime> DealsNow { get; set; } = () => DateTime.UtcNow;

    internal bool IsDealsRefreshScheduled => _dealsTimer?.IsEnabled == true;
    internal bool IsDealsAgeTimerRunning => _dealsAgeTimer?.IsEnabled == true;

    private void MarkDealsUpdated(DateTime utc)
    {
        if (_dealsUpdatedUtc is { } old && old >= utc) return;
        _dealsUpdatedUtc = utc;
        RaiseDealsUpdated();
    }

    public bool HasDealsUpdated => _dealsUpdatedUtc != null && !HasNoDeals;

    /// «آخر تحديث قبل 5 دقائق · يتحدّث كل ساعة»
    public string DealsUpdatedText
    {
        get
        {
            if (_dealsUpdatedUtc is not { } at) return "";
            var minutes = Math.Max(0, (int)(DealsNow() - at).TotalMinutes);
            var ago = minutes < 1 ? Services.Loc.Instance["dealsJustNow"]
                : minutes < 60 ? HalawaHub.App.BigPicture.BpPlural.MinutesAgo(minutes)
                : HalawaHub.App.BigPicture.BpPlural.HoursAgo(minutes / 60);
            return Services.Loc.Instance.Format("dealsUpdated", ago);
        }
    }

    private void RaiseDealsUpdated()
    {
        OnPropertyChanged(nameof(HasDealsUpdated));
        OnPropertyChanged(nameof(DealsUpdatedText));
    }

    /// يبدأ مع البرنامج: تحديث العروض والمجانيات كل ساعة
    private void StartDealsRefresh()
    {
        if (_dealsTimer != null) return;
        _dealsTimer = new Avalonia.Threading.DispatcherTimer { Interval = DealsRefreshInterval };
        _dealsTimer.Tick += (_, _) => _ = RefreshDealsAsync();
        _dealsTimer.Start();
    }

    internal async Task RefreshDealsAsync()
    {
        await LoadFreeGamesAsync();
        await LoadStoreDealsAsync();
    }

    private void UpdateDealsAgeTimer()
    {
        if (IsDealsPage)
        {
            RaiseDealsUpdated();
            _dealsAgeTimer ??= new Avalonia.Threading.DispatcherTimer(TimeSpan.FromMinutes(1), Avalonia.Threading.DispatcherPriority.Background,
                (_, _) => RaiseDealsUpdated());
            _dealsAgeTimer.Start();
        }
        else
        {
            _dealsAgeTimer?.Stop();
        }
    }
}
