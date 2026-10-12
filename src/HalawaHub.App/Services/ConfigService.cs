using System;
using System.IO;
using System.Text.Json;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

public static class ConfigService
{
    public static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "config.json");

    public static AppConfig Load() => LoadFrom(ConfigPath);

    public static void Save(AppConfig config) => SaveTo(ConfigPath, config);

    // ===== internals: seam للاختبارات (B04/E5) =====

    internal static AppConfig LoadFrom(string path)
    {
        if (!File.Exists(path))
        {
            // أول تشغيل فعلًا — ننشئ افتراضي
            var defaults = new AppConfig();
            SaveTo(path, defaults);
            return defaults;
        }

        // ملف موجود — نفصل أعطال IO عن أعطال JSON
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ملف مقفل — لا نلمسه، نرجع افتراضي بدون كتابة
            Log.Error($"config file unreadable (locked?): {ex.Message}", ex);
            return new AppConfig();
        }
        catch (Exception ex)
        {
            Log.Error($"config file read failed: {ex.Message}", ex);
            return new AppConfig();
        }

        AppConfig? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<AppConfig>(json);
        }
        catch (JsonException ex)
        {
            // JSON تالف (السيناريو E5) — نعزله وننشئ افتراضي
            Log.Error($"config.json corrupted — quarantining: {ex.Message}", ex);
            Quarantine(path);
            var defaults = new AppConfig();
            SaveTo(path, defaults);
            return defaults;
        }

        if (parsed is null)
        {
            // ملف فاضي أو JSON يعطي null
            Log.Error("config.json deserialized to null — quarantining");
            Quarantine(path);
            var defaults = new AppConfig();
            SaveTo(path, defaults);
            return defaults;
        }

        return parsed;
    }

    internal static void SaveTo(string path, AppConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var content = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            WriteAtomic(path, content);
        }
        catch (Exception ex)
        {
            Log.Error($"config save failed: {ex.Message}", ex);
        }
    }

    private static void Quarantine(string path)
    {
        try
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var quarantine = path + ".corrupt-" + stamp;
            File.Move(path, quarantine, overwrite: false);
            Log.Error($"quarantined corrupt config to: {quarantine}");
        }
        catch (Exception ex)
        {
            Log.Error($"quarantine failed: {ex.Message}", ex);
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }
}
