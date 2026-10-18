using System;
using Microsoft.Win32;

namespace HalawaHub.App.Services;

/// <summary>
/// يفعّل/يعطّل تشغيل البرنامج تلقائيًا مع بدء تشغيل ويندوز.
/// الميزة خاصة بويندوز (مفتاح Registry Run). على غير ويندوز:
///   IsEnabled() -> false، SetEnabled() -> no-op صامت.
/// الحراس (B28a) تُزيل تحذيرات CA1416 وتجعل السلوك عبر المنصات صريحًا
/// بدل الاعتماد على try/catch يبتلع الاستثناء بصمت (وثيقة CA1416:
/// المحلّل يتعرّف على حراس OperatingSystem class).
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HalawaHub";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                             ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }
        }
        catch
        {
            // فشل التعديل (صلاحيات مثلًا)، نتجاهل بهدوء
        }
    }
}
