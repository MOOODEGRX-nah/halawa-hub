using System;
using System.IO;
using System.Text.Json;

namespace HalawaHub.App.Services;

/// <summary>
/// يحفظ حجم/حالة النافذة بملف صغير عند الإغلاق، ويرجّعها عند فتح البرنامج
/// من جديد — بدل ما يفتح دايمًا بنفس الحجم الافتراضي.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "window-settings.json");

    public static WindowSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<WindowSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch
        {
            // ملف تالف أو غير قابل للقراءة، نرجع الإعدادات الافتراضية بهدوء
        }

        return new WindowSettings();
    }

    public static void Save(WindowSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // فشل الحفظ (صلاحيات، قرص ممتلئ...) — مو حرجة، نتجاهل
        }
    }
}
