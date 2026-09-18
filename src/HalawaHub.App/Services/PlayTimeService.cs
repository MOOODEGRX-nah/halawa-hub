using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// <summary>
/// يحفظ إجمالي ثواني اللعب لكل لعبة في playtime.json —
/// خدمة مستقلة صغيرة بنفس نمط باقي الخدمات.
/// </summary>
public static class PlayTimeService
{
    private static readonly object Lock = new();
    private static Dictionary<string, long>? _cache;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "playtime.json");

    private static Dictionary<string, long> Load()
    {
        if (_cache != null) return _cache;
        try
        {
            _cache = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch
        {
            _cache = new();
        }
        return _cache;
    }

    private static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_cache));
        }
        catch
        {
            // تجاهل — الخدمة تحسين وليست ضرورة
        }
    }

    public static TimeSpan GetTotal(string platform, string id)
    {
        lock (Lock)
        {
            var key = platform + "|" + id;
            return Load().TryGetValue(key, out var secs) ? TimeSpan.FromSeconds(secs) : TimeSpan.Zero;
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
}
