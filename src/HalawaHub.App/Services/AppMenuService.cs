using System;
using System.IO;
using HalawaHub.Core;
using HalawaHub.Core.Updates;

namespace HalawaHub.App.Services;

/// <summary>
/// مدخل قائمة التطبيقات لمن يشغّل ملف AppImage. سكربت tar.gz وحزمة AUR يكتبانه بأنفسهما،
/// أما AppImage فملف واحد بلا تثبيت، والكتابة في قائمة المستخدم تكون بطلبه لا تلقائيًا.
/// </summary>
public static class AppMenuService
{
    private const string MarkerLine = "X-Halawa-Hub-AppImage=true";
    private static readonly int[] IconSizes = { 16, 32, 48, 64, 128, 256 };

    public static bool IsSupported => InstallInfo.CurrentAppImage != null;

    public static bool IsEnabled() => IsSupported && IsOwnEntry(EntryPath());

    public static void SetEnabled(bool enabled)
    {
        var appImage = InstallInfo.CurrentAppImage;
        if (appImage == null) return;

        try
        {
            if (enabled)
            {
                LinuxDesktopEntry.WriteAtomically(EntryPath(), BuildEntry(appImage));
                CopyIcons();
            }
            else if (IsOwnEntry(EntryPath()))
            {
                // مدخل كتبه install.sh لتثبيت tar.gz ليس ملكًا لهذا الزر
                File.Delete(EntryPath());
            }
        }
        catch (Exception ex)
        {
            Log.Error("could not change the applications menu entry", ex);
        }
    }

    /// ملف AppImage نُقل بعد إضافته للقائمة: نصحح المسار كما في Autostart
    public static void RefreshPathIfEnabled()
    {
        var appImage = InstallInfo.CurrentAppImage;
        if (appImage == null || !IsEnabled()) return;

        try
        {
            var expected = BuildEntry(appImage);
            if (File.ReadAllText(EntryPath()) != expected)
                LinuxDesktopEntry.WriteAtomically(EntryPath(), expected);
        }
        catch (Exception ex)
        {
            Log.Error("could not refresh the applications menu entry", ex);
        }
    }

    internal static string BuildEntry(string appImagePath) =>
        LinuxDesktopEntry.Build(appImagePath) + MarkerLine + "\n";

    internal static bool IsOwnEntry(string path)
    {
        try
        {
            return File.Exists(path) && File.ReadAllText(path).Contains(MarkerLine, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string DataHome() => LinuxDesktopEntry.DataHome(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"), LinuxDesktopEntry.Home);

    private static string EntryPath() => Path.Combine(DataHome(), "applications", LinuxDesktopEntry.FileName);

    // الأيقونات داخل AppImage بجانب الملف التنفيذي؛ نسخها يبقيها متاحة والملف غير مركّب
    private static void CopyIcons()
    {
        var iconsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "share", "icons", "hicolor"));
        foreach (var size in IconSizes)
        {
            var source = Path.Combine(iconsRoot, $"{size}x{size}", "apps", LinuxDesktopEntry.IconName + ".png");
            if (!File.Exists(source)) continue;

            var target = Path.Combine(DataHome(), "icons", "hicolor", $"{size}x{size}", "apps", LinuxDesktopEntry.IconName + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }
}
