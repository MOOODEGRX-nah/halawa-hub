using HalawaHub.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// <summary>
/// يسجّل آخر وقت شغّل فيه المستخدم كل لعبة — البيانات الحقيقية اللي
/// يعتمد عليها "استمر من حيث توقفت" و"آخر الألعاب اللي لعبتها" بالواجهة.
/// الكتابة آمنة (ملف مؤقت ثم استبدال مع .bak) والأخطاء تُسجَّل.
/// </summary>
public static class PlayHistoryService
{
    private static readonly object Lock = new();
    private static readonly string FilePath = AppPaths.InData("play-history.json");

    private static readonly Dictionary<string, DateTime> History =
        AtomicFile.ReadWithBackup(FilePath,
            json => JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json), "play-history.json") ?? new();

    private static void Save()
    {
        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(History));
        }
        catch (Exception ex)
        {
            Log.Error("play-history.json: save failed", ex);
        }
    }

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public static void RecordPlayed(string platform, string id)
    {
        lock (Lock)
        {
            History[Key(platform, id)] = DateTime.UtcNow;
            Save();
        }
    }

    public static DateTime? GetLastPlayed(string platform, string id)
    {
        lock (Lock)
            return History.TryGetValue(Key(platform, id), out var time) ? time : null;
    }
}
