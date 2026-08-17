using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// <summary>
/// يحفظ قائمة الألعاب المفضّلة بملف صغير محلي — مستقلة عن نتائج الفحص
/// نفسها لأنها تفضيل شخصي للمستخدم، مو بيانات تجيء من المنصة.
/// </summary>
public static class FavoritesService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "favorites.json");

    private static readonly HashSet<string> Favorites = Load();

    private static HashSet<string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var items = JsonSerializer.Deserialize<List<string>>(json);
                if (items != null) return new HashSet<string>(items);
            }
        }
        catch
        {
            // ملف تالف، نبدأ بقائمة فاضية بهدوء
        }
        return new HashSet<string>();
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(new List<string>(Favorites));
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // فشل الحفظ، مو حرج
        }
    }

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public static bool IsFavorite(string platform, string id) => Favorites.Contains(Key(platform, id));

    public static void SetFavorite(string platform, string id, bool value)
    {
        var key = Key(platform, id);
        if (value) Favorites.Add(key);
        else Favorites.Remove(key);
        Save();
    }
}
