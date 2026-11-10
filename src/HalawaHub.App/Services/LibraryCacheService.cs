using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HalawaHub.Core;
using HalawaHub.Core.Models;

namespace HalawaHub.App.Services;

/// <summary>
/// آخر مكتبة معروفة (library-cache.json). لعبة على قرص غير موصول تختفي من فحص
/// المنصة (Steam يتخطى مكتبة لا يجدها)؛ نبقيها من هذا الملف بحالة "القرص غير موصول"
/// بدل أن تختفي. لعبة قرصها موصول ولم يجدها الفحص = أُزيلت فعلًا، فتُحذف من الملف.
/// </summary>
public static class LibraryCacheService
{
    public static string DefaultPath => AppPaths.InData("library-cache.json");

    public sealed class Entry
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Platform { get; set; } = "";
        public string InstallPath { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string? LaunchArguments { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageUrlFallback { get; set; }

        public static Entry From(GameInfo g) => new()
        {
            Id = g.Id, Name = g.Name, Platform = g.Platform, InstallPath = g.InstallPath,
            ExecutablePath = g.ExecutablePath, LaunchArguments = g.LaunchArguments,
            CoverImageUrl = g.CoverImageUrl, CoverImageUrlFallback = g.CoverImageUrlFallback
        };

        public GameInfo ToGame() => new()
        {
            Id = Id, Name = Name, Platform = Platform, InstallPath = InstallPath,
            ExecutablePath = ExecutablePath, LaunchArguments = LaunchArguments,
            CoverImageUrl = CoverImageUrl, CoverImageUrlFallback = CoverImageUrlFallback, IsInstalled = true
        };
    }

    /// الفحص الجديد + ألعاب الفحوص السابقة التي قرصها غير موصول الآن، ويحفظ الناتج
    public static List<GameInfo> MergeOffline(string path, IReadOnlyList<GameInfo> scanned, Func<string?, bool> isDriveMissing)
    {
        var cached = AtomicFile.ReadWithBackup(path, json => JsonSerializer.Deserialize<List<Entry>>(json), "library-cache.json")
                     ?? new List<Entry>();

        var seen = new HashSet<string>(scanned.Select(g => g.Platform + "|" + g.Id));
        var result = scanned.ToList();
        foreach (var entry in cached)
        {
            if (seen.Contains(entry.Platform + "|" + entry.Id)) continue;
            if (!isDriveMissing(entry.InstallPath)) continue;
            result.Add(entry.ToGame());
            seen.Add(entry.Platform + "|" + entry.Id);
        }

        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(result.Select(Entry.From).ToList()));
        }
        catch (Exception ex)
        {
            Log.Error("library-cache.json: save failed", ex);
        }
        return result;
    }
}
