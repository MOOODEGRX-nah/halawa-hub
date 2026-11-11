using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// شرط واحد في مجموعة ذكية
public sealed class CollectionRule
{
    /// platform, playtime, lastplayed, added, installed, favorite, name
    public string Field { get; set; } = "platform";

    /// platform: is/isnot · playtime: atleast/atmost (ساعات) · lastplayed: within/notwithin (أيام)/never
    /// added: within (أيام) · installed/favorite: yes/no · name: contains
    public string Op { get; set; } = "is";
    public string Value { get; set; } = "";
}

public sealed class GameCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";

    /// مفتاح أيقونة من CollectionIcons
    public string Icon { get; set; } = "star";
    public bool IsSmart { get; set; }
    public bool Hidden { get; set; }

    /// يدوية: مفاتيح الألعاب "Platform|Id"
    public List<string> Games { get; set; } = new();

    /// ذكية: كل الشروط (true) أو أي شرط (false)
    public bool MatchAll { get; set; } = true;
    public List<CollectionRule> Rules { get; set; } = new();
}

/// ما تحتاجه الشروط من اللعبة — بيانات يملكها Halawa فعلًا
public sealed record GameFacts(string Key, string Platform, string Name, TimeSpan PlayTime,
    DateTime? LastPlayedUtc, DateTime AddedUtc, bool Installed, bool Favorite);

/// <summary>
/// المجموعات (collections.json): يدوية يختار المستخدم ألعابها، وذكية تدخلها الألعاب
/// وتخرج منها حسب شروط. ترتيب القائمة هو ترتيب الظهور.
/// </summary>
public static class CollectionsService
{
    public static string DefaultPath => AppPaths.InData("collections.json");

    public static readonly string[] Fields = { "platform", "playtime", "lastplayed", "added", "installed", "favorite", "name" };

    public static string[] OpsFor(string field) => field switch
    {
        "platform" => new[] { "is", "isnot" },
        "playtime" => new[] { "atleast", "atmost" },
        "lastplayed" => new[] { "within", "notwithin", "never" },
        "added" => new[] { "within" },
        "installed" or "favorite" => new[] { "yes", "no" },
        _ => new[] { "contains" }
    };

    /// هل يحتاج هذا الشرط قيمة يكتبها المستخدم؟
    public static bool NeedsValue(string field, string op) =>
        field is "platform" or "playtime" or "added" or "name" || (field == "lastplayed" && op != "never");

    public static List<GameCollection> Load(string path) =>
        AtomicFile.ReadWithBackup(path, json => JsonSerializer.Deserialize<List<GameCollection>>(json), "collections.json")
        ?? new List<GameCollection>();

    public static void Save(string path, List<GameCollection> collections)
    {
        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(collections, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log.Error("collections.json: save failed", ex);
        }
    }

    /// شرط مكتمل وصالح (حقل معروف، مقارنة تخصه، وقيمة رقمية حيث تلزم)
    public static bool IsValid(CollectionRule r)
    {
        if (!Fields.Contains(r.Field) || !OpsFor(r.Field).Contains(r.Op)) return false;
        if (!NeedsValue(r.Field, r.Op)) return true;
        if (string.IsNullOrWhiteSpace(r.Value)) return false;
        return r.Field is "platform" or "name" || (TryNumber(r.Value, out var n) && n >= 0);
    }

    /// <summary>
    /// يمكن حفظ المجموعة؟ تحتاج اسمًا، والذكية تحتاج شرطًا صالحًا واحدًا على الأقل
    /// وكل شروطها صالحة (مجموعة ذكية بلا شروط كانت ستضم كل المكتبة بصمت).
    /// </summary>
    public static bool CanSave(GameCollection c) =>
        !string.IsNullOrWhiteSpace(c.Name) && (!c.IsSmart || (c.Rules.Count > 0 && c.Rules.All(IsValid)));

    public static bool Contains(GameCollection c, GameFacts g, DateTime utcNow)
    {
        if (!c.IsSmart) return c.Games.Contains(g.Key);
        if (c.Rules.Count == 0) return false;
        return c.MatchAll ? c.Rules.All(r => Matches(r, g, utcNow)) : c.Rules.Any(r => Matches(r, g, utcNow));
    }

    public static bool Matches(CollectionRule r, GameFacts g, DateTime utcNow)
    {
        if (!IsValid(r)) return false;
        TryNumber(r.Value, out var n);
        switch (r.Field)
        {
            case "platform":
                var same = string.Equals(g.Platform, r.Value.Trim(), StringComparison.OrdinalIgnoreCase);
                return r.Op == "is" ? same : !same;
            case "playtime":
                return r.Op == "atleast" ? g.PlayTime.TotalHours >= n : g.PlayTime.TotalHours <= n;
            case "lastplayed":
                if (r.Op == "never") return g.LastPlayedUtc == null;
                var recent = g.LastPlayedUtc != null && (utcNow - g.LastPlayedUtc.Value).TotalDays <= n;
                return r.Op == "within" ? recent : !recent;
            case "added":
                return (utcNow - g.AddedUtc).TotalDays <= n;
            case "installed":
                return g.Installed == (r.Op == "yes");
            case "favorite":
                return g.Favorite == (r.Op == "yes");
            default:
                return g.Name.Contains(r.Value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool TryNumber(string s, out double n) =>
        double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out n);
}
