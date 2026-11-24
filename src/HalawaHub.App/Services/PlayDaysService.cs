using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// وقت اللعب لكل يوم ولكل لعبة، وآخر جلسة (لـ«لعبك هذا الأسبوع» و«أيام متتالية» في الشاشة الكبيرة).
/// ملف جديد play-days.json بجانب playtime.json: الإعدادات القديمة تُقرأ كما هي، وما قبل هذا الإصدار
/// لا أيام له. يُسجَّل فقط ما يتتبعه Halawa بنفسه؛ وقت Steam المستورد إجمالي بلا أيام.
/// يُحفظ آخر 60 يومًا فقط.
/// </summary>
public static class PlayDaysService
{
    public sealed class GameDays
    {
        public Dictionary<string, long> Days { get; set; } = new();
        public DateTime? LastEnd { get; set; }
        public long LastSeconds { get; set; }
    }

    private const int KeepDays = 60;
    private static readonly object Lock = new();
    private static Dictionary<string, GameDays>? _cache;

    private static string FilePath => AppPaths.InData("play-days.json");

    private static Dictionary<string, GameDays> Load()
    {
        if (_cache != null) return _cache;
        _cache = AtomicFile.ReadWithBackup(FilePath,
            json => JsonSerializer.Deserialize<Dictionary<string, GameDays>>(json), "play-days.json") ?? new();
        return _cache;
    }

    private static string Day(DateTime local) => local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// جلسة انتهت (بالتوقيت المحلي). جلسة تعبر منتصف الليل تُقسم على اليومين.
    public static void Record(string platform, string id, DateTime endLocal, double seconds)
    {
        if (seconds < 5) return;
        lock (Lock)
        {
            var all = Load();
            var key = platform + "|" + id;
            if (!all.TryGetValue(key, out var g)) all[key] = g = new GameDays();

            var start = endLocal.AddSeconds(-seconds);
            var cursor = start;
            while (cursor < endLocal)
            {
                var next = Math.Min(cursor.Date.AddDays(1).Ticks, endLocal.Ticks);
                var part = (long)Math.Round(TimeSpan.FromTicks(next - cursor.Ticks).TotalSeconds);
                var d = Day(cursor);
                g.Days[d] = (g.Days.TryGetValue(d, out var s) ? s : 0) + part;
                cursor = new DateTime(next, endLocal.Kind);
            }
            g.LastEnd = endLocal;
            g.LastSeconds = (long)Math.Round(seconds);

            var oldest = Day(endLocal.Date.AddDays(-KeepDays));
            foreach (var old in g.Days.Keys.Where(k => string.CompareOrdinal(k, oldest) < 0).ToList()) g.Days.Remove(old);

            try { AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(all)); }
            catch (Exception ex) { Log.Error("play-days.json: save failed", ex); }
        }
    }

    /// ساعات آخر 7 أيام، الأقدم أولًا وآخرها اليوم
    public static double[] Week(string platform, string id, DateTime todayLocal)
    {
        lock (Lock)
        {
            Load().TryGetValue(platform + "|" + id, out var g);
            return Enumerable.Range(0, 7)
                .Select(i => Day(todayLocal.Date.AddDays(i - 6)))
                .Select(d => g != null && g.Days.TryGetValue(d, out var s) ? s / 3600.0 : 0)
                .ToArray();
        }
    }

    /// أيام متتالية لُعبت فيها اللعبة، تنتهي اليوم أو أمس (لم ينقطع بعد)
    public static int Streak(string platform, string id, DateTime todayLocal)
    {
        lock (Lock)
        {
            if (!Load().TryGetValue(platform + "|" + id, out var g)) return 0;
            var day = todayLocal.Date;
            if (!g.Days.ContainsKey(Day(day))) day = day.AddDays(-1);
            var n = 0;
            while (g.Days.TryGetValue(Day(day), out var s) && s > 0) { n++; day = day.AddDays(-1); }
            return n;
        }
    }

    public static (DateTime End, TimeSpan Length)? LastSession(string platform, string id)
    {
        lock (Lock)
        {
            return Load().TryGetValue(platform + "|" + id, out var g) && g.LastEnd is { } end
                ? (end, TimeSpan.FromSeconds(g.LastSeconds))
                : null;
        }
    }

    /// للاختبارات
    internal static void ResetForTests() { lock (Lock) _cache = null; }
}
