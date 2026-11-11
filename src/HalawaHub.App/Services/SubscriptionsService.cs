using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// اشتراك يتابعه المستخدم محليًا (Game Pass, EA Play...). لا اتصال بأي خدمة.
public sealed class Subscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public decimal Cost { get; set; }

    /// "monthly" أو "yearly"
    public string Cycle { get; set; } = "monthly";

    /// تاريخ تجديد معروف (أي تجديد سابق أو قادم)؛ التجديد القادم يُحسب منه
    public DateTime Renewal { get; set; }
}

public static class SubscriptionsService
{
    public static string DefaultPath => AppPaths.InData("subscriptions.json");

    public static List<Subscription> Load(string path) =>
        AtomicFile.ReadWithBackup(path, json => JsonSerializer.Deserialize<List<Subscription>>(json), "subscriptions.json")
        ?? new List<Subscription>();

    public static void Save(string path, List<Subscription> items)
    {
        try { AtomicFile.WriteAllText(path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Log.Error("subscriptions.json: save failed", ex); }
    }

    /// المجموع الشهري: السنوي يُقسم على 12
    public static decimal MonthlyTotal(IEnumerable<Subscription> items) =>
        Math.Round(items.Sum(s => s.Cycle == "yearly" ? s.Cost / 12m : s.Cost), 2);

    /// أول تجديد في اليوم أو بعده. يُحسب دائمًا من التاريخ المعروف نفسه (لا بالتراكم)
    /// حتى لا ينزاح يوم 31 إلى 28 بعد المرور بشهر فبراير.
    public static DateTime NextRenewal(Subscription s, DateTime today)
    {
        var known = s.Renewal.Date;
        today = today.Date;
        var step = s.Cycle == "yearly" ? 12 : 1;
        var k = ((today.Year - known.Year) * 12 + today.Month - known.Month) / step;
        while (known.AddMonths(k * step) < today) k++;
        while (known.AddMonths((k - 1) * step) >= today) k--;
        return known.AddMonths(k * step);
    }
}
