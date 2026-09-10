using System;
using Microsoft.Win32;

namespace HalawaHub.App.Services;

/// يفعّل/يعطّل تشغيل البرنامج تلقائيًا مع بدء تشغيل ويندوز
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HalawaHub";

    public static bool IsEnabled()
    {
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
            // فشل التعديل (صلاحيات مثلاً)، نتجاهل بهدوء
        }
    }
}
