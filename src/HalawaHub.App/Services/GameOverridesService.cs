using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// اسم العرض المخصص وإخفاء اللعبة: يُحفظان لكل لعبة في ملفين جديدين بجانب
/// launch-params.json. عدم وجود الملفين (بيانات قديمة) يعني لا تخصيص.
/// </summary>
public static class GameOverridesService
{
    private static readonly string NamesPath = AppPaths.InData("display-names.json");
    private static readonly string HiddenPath = AppPaths.InData("hidden-games.json");

    private static readonly Dictionary<string, string> Names = LoadNames();
    private static readonly HashSet<string> Hidden = LoadHidden();

    private static string Key(string platform, string id) => $"{platform}|{id}";

    public static string? GetDisplayName(string platform, string id) =>
        Names.TryGetValue(Key(platform, id), out var value) ? value : null;

    /// <summary>اسم فارغ يحذف التخصيص ويعيد الاسم الأصلي.</summary>
    public static void SetDisplayName(string platform, string id, string? name)
    {
        var key = Key(platform, id);
        if (string.IsNullOrWhiteSpace(name)) Names.Remove(key);
        else Names[key] = name.Trim();
        Write(NamesPath, Names);
    }

    public static bool IsHidden(string platform, string id) => Hidden.Contains(Key(platform, id));

    public static void SetHidden(string platform, string id, bool hidden)
    {
        var key = Key(platform, id);
        if (hidden) Hidden.Add(key);
        else Hidden.Remove(key);
        Write(HiddenPath, new List<string>(Hidden));
    }

    private static Dictionary<string, string> LoadNames() =>
        Read<Dictionary<string, string>>(NamesPath) ?? new Dictionary<string, string>();

    private static HashSet<string> LoadHidden() =>
        new(Read<List<string>>(HiddenPath) ?? new List<string>());

    private static T? Read<T>(string path) where T : class
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Log.Error($"could not read {Path.GetFileName(path)}", ex);
        }
        return null;
    }

    private static void Write<T>(string path, T value)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(value));
        }
        catch (Exception ex)
        {
            Log.Error($"could not save {Path.GetFileName(path)}", ex);
        }
    }
}
