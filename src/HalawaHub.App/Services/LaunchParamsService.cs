using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// يحفظ معاملات تشغيل مخصصة يضيفها المستخدم لكل لعبة (زي Launch Options بـ Steam)
public static class LaunchParamsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "launch-params.json");

    private static readonly Dictionary<string, string> Params = Load();

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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Params));
        }
        catch
        {
            // فشل الحفظ، مو حرج
        }
    }

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public static string Get(string platform, string id) =>
        Params.TryGetValue(Key(platform, id), out var value) ? value : "";

    public static void Set(string platform, string id, string value)
    {
        var key = Key(platform, id);
        if (string.IsNullOrWhiteSpace(value)) Params.Remove(key);
        else Params[key] = value;
        Save();
    }
}
