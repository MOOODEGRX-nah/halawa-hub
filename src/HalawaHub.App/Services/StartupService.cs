using System;
using System.IO;
using System.Linq;
using HalawaHub.Core;
using HalawaHub.Core.Updates;
using Microsoft.Win32;

namespace HalawaHub.App.Services;

/// <summary>
/// يفعّل/يعطّل تشغيل البرنامج تلقائيًا مع بدء النظام:
///   ويندوز: مفتاح Registry Run.
///   لينكس: ملف XDG Autostart في ~/.config/autostart/halawa-hub.desktop.
/// على غيرهما: IsEnabled() -> false، SetEnabled() -> no-op صامت.
/// الحراس (B28a) تُزيل تحذيرات CA1416 وتجعل السلوك عبر المنصات صريحًا
/// بدل الاعتماد على try/catch يبتلع الاستثناء بصمت (وثيقة CA1416:
/// المحلّل يتعرّف على حراس OperatingSystem class).
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HalawaHub";

    public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    public static bool IsEnabled()
    {
        if (OperatingSystem.IsLinux()) return IsAutostartEnabled(AutostartPath());
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
        if (OperatingSystem.IsLinux())
        {
            SetAutostart(AutostartPath(), enabled, InstallInfo.LauncherPath);
            return;
        }

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

    /// <summary>
    /// ملف AppImage يُنقل بسهولة بين المجلدات، فنصحح المسار إذا اختفى الملف الذي يشير
    /// إليه، وإلا يفشل التشغيل التلقائي بصمت في الجلسة القادمة. لا نغيّره وهو صالح:
    /// تشغيل AppImage مرة للتجربة لا يسرق التشغيل التلقائي من تثبيت tar.gz.
    /// </summary>
    public static void RefreshPathIfEnabled()
    {
        if (OperatingSystem.IsLinux())
            RefreshAutostart(AutostartPath(), InstallInfo.LauncherPath);
    }

    // ===== لينكس =====

    private static string AutostartPath() => AutostartPath(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), LinuxDesktopEntry.Home);

    internal static string AutostartPath(string? xdgConfigHome, string home) =>
        Path.Combine(LinuxDesktopEntry.ConfigHome(xdgConfigHome, home), "autostart", LinuxDesktopEntry.FileName);

    /// مدير الجلسة قد يعطّل الملف بـ Hidden=true بدل حذفه، فالملف وحده لا يكفي
    internal static bool IsAutostartEnabled(string path)
    {
        try
        {
            return File.Exists(path) && !File.ReadLines(path).Any(line =>
                line.Trim().Equals("Hidden=true", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    internal static void SetAutostart(string path, bool enabled, string? launcherPath)
    {
        try
        {
            if (!enabled)
            {
                File.Delete(path);
            }
            else if (!string.IsNullOrEmpty(launcherPath))
            {
                LinuxDesktopEntry.WriteAtomically(path, LinuxDesktopEntry.Build(launcherPath));
            }
        }
        catch (Exception ex)
        {
            Log.Error("could not change the autostart entry", ex);
        }
    }

    internal static void RefreshAutostart(string path, string? launcherPath)
    {
        if (string.IsNullOrEmpty(launcherPath) || !IsAutostartEnabled(path)) return;

        try
        {
            var current = LinuxDesktopEntry.ReadExecPath(File.ReadAllText(path));
            if (current == null || !File.Exists(current))
                LinuxDesktopEntry.WriteAtomically(path, LinuxDesktopEntry.Build(launcherPath));
        }
        catch (Exception ex)
        {
            Log.Error("could not refresh the autostart entry", ex);
        }
    }
}
