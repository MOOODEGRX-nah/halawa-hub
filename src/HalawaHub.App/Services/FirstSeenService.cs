using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HalawaHub.Core;
using HalawaHub.Core.Models;

namespace HalawaHub.App.Services;

/// <summary>
/// "تاريخ الإضافة" لكل لعبة (first-seen.json): أول مرة رآها Halawa. اللعبة التي كانت
/// موجودة قبل هذه الميزة تأخذ تاريخ إنشاء مجلدها، وهو أقرب ما نعرفه لتاريخ تثبيتها.
/// </summary>
public static class FirstSeenService
{
    private static readonly object Lock = new();
    private static Dictionary<string, DateTime>? _cache;
    private static bool _dirty;

    internal static string FilePath => AppPaths.InData("first-seen.json");

    private static Dictionary<string, DateTime> Load() =>
        _cache ??= AtomicFile.ReadWithBackup(FilePath,
            json => JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json), "first-seen.json") ?? new();

    public static DateTime Get(GameInfo game)
    {
        lock (Lock)
        {
            var data = Load();
            var key = game.Platform + "|" + game.Id;
            if (data.TryGetValue(key, out var seen)) return seen;

            var now = DateTime.UtcNow;
            var value = now;
            try
            {
                if (!string.IsNullOrEmpty(game.InstallPath) && Directory.Exists(game.InstallPath))
                {
                    var created = Directory.GetCreationTimeUtc(game.InstallPath);
                    if (created > new DateTime(2000, 1, 1) && created < now) value = created;
                }
            }
            catch
            {
                // مجلد غير قابل للقراءة: "الآن"
            }

            data[key] = value;
            _dirty = true;
            return value;
        }
    }

    /// يُستدعى مرة بعد كل فحص بدل حفظ عند كل لعبة
    public static void SaveIfChanged()
    {
        lock (Lock)
        {
            if (!_dirty || _cache == null) return;
            try
            {
                AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(_cache));
                _dirty = false;
            }
            catch (Exception ex)
            {
                Log.Error("first-seen.json: save failed", ex);
            }
        }
    }
}
