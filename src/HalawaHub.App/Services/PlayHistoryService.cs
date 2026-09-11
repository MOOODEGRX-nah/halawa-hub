using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// <summary>
/// يسجّل آخر وقت شغّل فيه المستخدم كل لعبة — البيانات الحقيقية اللي
/// يعتمد عليها "استمر من حيث توقفت" و"آخر الألعاب اللي لعبتها" بالواجهة.
/// أول مرة يشتغل فيها البرنامج ما فيه سجل طبعًا (بيبدأ يتكوّن من هذي اللحظة).
/// </summary>
public static class PlayHistoryService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "play-history.json");

    private static readonly Dictionary<string, DateTime> History = Load();

    private static Dictionary<string, DateTime> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var items = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
                if (items != null) return items;
            }
        }
        catch
        {
            // ملف تالف، نبدأ سجل فاضي بهدوء
        }
        return new Dictionary<string, DateTime>();
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(History));
        }
        catch
        {
            // فشل الحفظ، مو حرج
        }
    }

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public static void RecordPlayed(string platform, string id)
    {
        History[Key(platform, id)] = DateTime.UtcNow;
        Save();
    }

    public static DateTime? GetLastPlayed(string platform, string id) =>
        History.TryGetValue(Key(platform, id), out var time) ? time : null;
}
