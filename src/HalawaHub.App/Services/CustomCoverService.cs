using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// يحفظ مسار غلاف مخصص (صورة محلية اختارها المستخدم يدويًا) لكل لعبة
public static class CustomCoverService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "custom-covers.json");

    private static readonly Dictionary<string, string> Covers = Load();

    private static Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var items = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (items != null) return items;
            }
        }
        catch
        {
            // ملف تالف، نبدأ فاضي بهدوء
        }
        return new Dictionary<string, string>();
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Covers));
        }
        catch
        {
            // فشل الحفظ، مو حرج
        }
    }

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public static string? Get(string platform, string id) =>
        Covers.TryGetValue(Key(platform, id), out var value) ? value : null;

    public static void Set(string platform, string id, string localFilePath)
    {
        Covers[Key(platform, id)] = localFilePath;
        Save();
    }
}
