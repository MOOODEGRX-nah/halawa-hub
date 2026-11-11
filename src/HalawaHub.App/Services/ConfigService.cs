using System;
using System.IO;
using System.Text.Json;
using HalawaHub.Core;
using HalawaHub.Core.Security;

namespace HalawaHub.App.Services;

public static class ConfigService
{
    public static readonly string ConfigPath = AppPaths.InData("config.json");

    public static AppConfig Load() => LoadFrom(ConfigPath);

    public static void Save(AppConfig config) => SaveTo(ConfigPath, config);

    // ===== internals: seam للاختبارات (B04/E5) =====

    internal static AppConfig LoadFrom(string path)
    {
        if (!File.Exists(path))
        {
            // أول تشغيل فعلًا — ننشئ افتراضي
            var defaults = new AppConfig { ShowOnboarding = true };
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

        LoadSecrets(path, parsed);
        return parsed;
    }

    internal const string SteamGridDbSecretName = "steamgriddb-api-key";

    /// <summary>
    /// المفتاح من مخزن الأسرار. ملف قديم فيه المفتاح نصًا صريحًا يُرحَّل فورًا:
    /// يُحفظ مشفّرًا ويُحذف النص من config.json.
    /// </summary>
    private static void LoadSecrets(string path, AppConfig config)
    {
        if (!string.IsNullOrEmpty(config.LegacyPlainSteamGridDbApiKey))
        {
            config.SteamGridDbApiKey = config.LegacyPlainSteamGridDbApiKey!;
            config.LegacyPlainSteamGridDbApiKey = null;
            Log.Info("config.json: migrating the SteamGridDB key to the secret store");
            SaveTo(path, config);
            return;
        }

        if (string.IsNullOrEmpty(config.SteamGridDbApiKeyRef)) return;

        var key = SecretStore.Load(config.SteamGridDbApiKeyRef);
        if (key == null)
        {
            // مخزن مقفل أو حساب ويندوز آخر: لا نمسح المرجع، فقد يعمل في المرة القادمة
            Log.Error("SteamGridDB key could not be read from the secret store");
            return;
        }
        config.SteamGridDbApiKey = key;
        config.SavedSteamGridDbApiKey = key;
    }

    private static void ProtectSecrets(AppConfig config)
    {
        config.LegacyPlainSteamGridDbApiKey = null;
        if (config.SavedSteamGridDbApiKey == config.SteamGridDbApiKey && config.SteamGridDbApiKeyRef != null) return;
        if (config.SavedSteamGridDbApiKey == null && config.SteamGridDbApiKey.Length == 0) return;

        config.SteamGridDbApiKeyRef = SecretStore.Save(SteamGridDbSecretName, config.SteamGridDbApiKey);
        if (config.SteamGridDbApiKeyRef.Length == 0) config.SteamGridDbApiKeyRef = null;
        config.SavedSteamGridDbApiKey = config.SteamGridDbApiKey;
    }

    internal static void SaveTo(string path, AppConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            ProtectSecrets(config);

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
