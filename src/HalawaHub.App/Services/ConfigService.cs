using System;
using System.IO;
using System.Text.Json;

namespace HalawaHub.App.Services;

public static class ConfigService
{
    public static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null) return config;
            }
        }
        catch
        {
            // ملف تالف، نتجاهل ونرجع الإعدادات الافتراضية
        }

        // أول تشغيل: ننشئ ملف فاضي جاهز يعدّله المستخدم يدويًا لو حاب
        var defaultConfig = new AppConfig();
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // فشل الإنشاء، مو حرج — البرنامج يشتغل عادي بدون الميزة الاختيارية
        }

        return defaultConfig;
    }

    public static void Save(AppConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // فشل الحفظ، مو حرج
        }
    }
}
