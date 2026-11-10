using HalawaHub.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// <summary>
/// يحفظ إجمالي ثواني اللعب لكل لعبة في playtime.json (نفس الصيغة القديمة).
/// الكتابة آمنة (ملف مؤقت ثم استبدال مع .bak) والأخطاء تُسجَّل بدل أن تُبتلع.
/// </summary>
public static class PlayTimeService
{
    private static readonly object Lock = new();
    private static Dictionary<string, long>? _cache;
    private static Dictionary<string, long> _external = new();

    /// وقت لعب من مصدر خارجي (Steam) بمفاتيح "Platform|Id"؛ قاموس فارغ يلغيه
    public static void SetExternal(Dictionary<string, long> seconds)
    {
        lock (Lock) _external = seconds;
    }

    private static string FilePath => AppPaths.InData("playtime.json");

    private static Dictionary<string, long> Load()
    {
        if (_cache != null) return _cache;
        _cache = AtomicFile.ReadWithBackup(FilePath,
            json => JsonSerializer.Deserialize<Dictionary<string, long>>(json), "playtime.json") ?? new();
        return _cache;
    }

    private static void Save()
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(_cache));
        }
        catch (Exception ex)
        {
            Log.Error("playtime.json: save failed", ex);
        }
    }

    public static TimeSpan GetTotal(string platform, string id)
    {
        lock (Lock)
        {
            var key = platform + "|" + id;
            var own = Load().TryGetValue(key, out var secs) ? secs : 0;
            // وقت Steam المستورد (إن فُعّل): نعرض الأكبر، ولا نكتبه في playtime.json
            if (_external.TryGetValue(key, out var ext) && ext > own) own = ext;
            return TimeSpan.FromSeconds(own);
        }
    }

    public static void AddSeconds(string platform, string id, double seconds)
    {
        if (seconds < 5) return; // جلسة أقل من 5 ثواني لا تُحتسب
        lock (Lock)
        {
            var key = platform + "|" + id;
            var data = Load();
            data[key] = (data.TryGetValue(key, out var old) ? old : 0) + (long)seconds;
            Save();
        }
    }

    /// للاختبارات: يعيد القراءة من القرص
    internal static void ResetCacheForTests()
    {
        lock (Lock) _cache = null;
    }
}
