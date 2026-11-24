using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// صور SteamGridDB المختارة لكل لعبة غير الغلاف: الخلفية (hero) والغلاف العريض (wide) والشعار (logo) والأيقونة (icon)،
/// ومصدر الغلاف الحالي (coverSource: own أو sgdb:&lt;style&gt;). ملف جديد custom-art.json بجانب custom-covers.json،
/// فالإعدادات القديمة تُقرأ كما هي، وغياب الملف يعني «لا شيء مخصص».
/// </summary>
public static class CustomArtService
{
    public const string Hero = "hero", Wide = "wide", Logo = "logo", Icon = "icon", CoverSource = "coverSource";

    private static string FilePath => AppPaths.InData("custom-art.json");

    private static Dictionary<string, Dictionary<string, string>>? _items;
    private static Dictionary<string, Dictionary<string, string>> Items => _items ??= Load();

    private static Dictionary<string, Dictionary<string, string>> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex)
        {
            Log.Error("custom-art.json unreadable, starting empty", ex);
        }
        return new();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(Items));
        }
        catch (Exception ex)
        {
            Log.Error("could not save custom-art.json", ex);
        }
    }

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public static string? Get(string platform, string id, string kind) =>
        Items.TryGetValue(Key(platform, id), out var art) && art.TryGetValue(kind, out var value) ? value : null;

    public static void Set(string platform, string id, string kind, string? value)
    {
        var key = Key(platform, id);
        if (value == null)
        {
            if (Items.TryGetValue(key, out var art) && art.Remove(kind))
            {
                if (art.Count == 0) Items.Remove(key);
                Save();
            }
            return;
        }
        if (!Items.TryGetValue(key, out var existing)) Items[key] = existing = new Dictionary<string, string>();
        existing[kind] = value;
        Save();
    }

    /// للاختبارات: إعادة القراءة من المجلد الحالي
    internal static void ResetForTests() => _items = null;
}
