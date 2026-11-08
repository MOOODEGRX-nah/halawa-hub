using System.IO;

namespace HalawaHub.Core.Updates;

/// <summary>طريقة تثبيت النسخة التي تعمل الآن، ومنها نقرر هل تحدّث نفسها.</summary>
public enum InstallKind
{
    Windows,

    /// tar.gz في مجلد قابل للكتابة: تحديث ذاتي باستبدال المجلد
    LinuxTarball,

    /// ملف AppImage قابل للكتابة: تحديث ذاتي باستبدال الملف
    LinuxAppImage,

    /// AUR أو حزمة نظام أو مجلد للقراءة فقط أو بناء مطور: إشعار وفتح صفحة الإصدار فقط
    NotifyOnly
}

public static class InstallInfo
{
    public const string TarballAssetName = "Halawa-Hub-linux-x64.tar.gz";
    public const string AppImageAssetName = "Halawa-Hub-x86_64.AppImage";

    /// <summary>
    /// ملف علامة داخل tar.gz: وجوده هو ما يسمح بالتحديث الذاتي، فبناء المطور وحزمة AUR
    /// (التي تحذفه) لا يستبدلان نفسيهما أبدًا حتى لو كان المجلد قابلًا للكتابة.
    /// </summary>
    public const string TarballMarkerFile = ".halawa-hub-tarball";

    // تُقرأ مرة واحدة قبل تنظيف البيئة في ScrubAppImageEnvironment
    private static readonly string? AppImagePath = ReadOwnAppImagePath(
        Environment.GetEnvironmentVariable("APPIMAGE"),
        Environment.GetEnvironmentVariable("APPDIR"),
        AppContext.BaseDirectory);

    public static InstallKind Current
    {
        get
        {
            if (OperatingSystem.IsWindows()) return InstallKind.Windows;
            if (!OperatingSystem.IsLinux()) return InstallKind.NotifyOnly;
            return DetectLinux(AppImagePath, AppContext.BaseDirectory, CanWriteToDirectory);
        }
    }

    /// <summary>مسار ملف AppImage الذي يعمل منه البرنامج، أو null لغير ذلك.</summary>
    public static string? CurrentAppImage => AppImagePath;

    /// <summary>
    /// المسار الذي يُشغَّل منه البرنامج في المرة القادمة (Autostart وقائمة التطبيقات).
    /// داخل AppImage مسار العملية مجلد مؤقت يتغير كل تشغيل، فالمطلوب ملف AppImage نفسه.
    /// </summary>
    public static string? LauncherPath => AppImagePath ?? Environment.ProcessPath;

    /// <summary>
    /// يمسح متغيرات AppImage من بيئة العملية حتى لا ترثها الألعاب والمشغّلات التي نفتحها:
    /// تطبيق Electron مثلًا يقرأ APPIMAGE في تحديثه التلقائي فيظن أن ملفنا ملفه.
    /// </summary>
    public static void ScrubAppImageEnvironment()
    {
        _ = AppImagePath; // يضمن قراءة القيمة قبل المسح
        foreach (var name in new[] { "APPIMAGE", "APPDIR", "ARGV0", "OWD" })
            Environment.SetEnvironmentVariable(name, null);
    }

    /// <summary>
    /// APPIMAGE وحده لا يكفي: تطبيق AppImage آخر قد يشغّلنا فنرث متغيراته،
    /// ولو صدّقناه لاستبدل التحديث ملف ذلك التطبيق. نتأكد أننا داخل APPDIR فعلًا.
    /// </summary>
    internal static string? ReadOwnAppImagePath(string? appImage, string? appDir, string baseDirectory)
    {
        if (string.IsNullOrEmpty(appImage) || string.IsNullOrEmpty(appDir)) return null;

        var root = appDir.TrimEnd('/') + "/";
        return baseDirectory.StartsWith(root, StringComparison.Ordinal) ? appImage : null;
    }

    internal static InstallKind DetectLinux(string? appImagePath, string baseDirectory, Func<string, bool> canWrite)
    {
        if (!string.IsNullOrEmpty(appImagePath))
        {
            var folder = Path.GetDirectoryName(appImagePath);
            return File.Exists(appImagePath) && !string.IsNullOrEmpty(folder) && canWrite(folder)
                ? InstallKind.LinuxAppImage
                : InstallKind.NotifyOnly;
        }

        var installDir = baseDirectory.TrimEnd('/');
        if (installDir.Length == 0 || !File.Exists(Path.Combine(installDir, TarballMarkerFile)))
            return InstallKind.NotifyOnly;

        // الاستبدال يعيد تسمية المجلد نفسه، فهو يحتاج الكتابة في المجلد الأب أيضًا
        var parent = Path.GetDirectoryName(installDir);
        return !string.IsNullOrEmpty(parent) && canWrite(installDir) && canWrite(parent)
            ? InstallKind.LinuxTarball
            : InstallKind.NotifyOnly;
    }

    /// <summary>null يعني إشعارًا فقط: لا يوجد ملف يحدّث هذا التثبيت.</summary>
    public static string? AssetNameFor(InstallKind kind) => kind switch
    {
        InstallKind.Windows => UpdateChecker.WindowsAssetName,
        InstallKind.LinuxTarball => TarballAssetName,
        InstallKind.LinuxAppImage => AppImageAssetName,
        _ => null
    };

    // محاولة كتابة حقيقية: تغطي صلاحيات المجموعات والأقراص المركبة للقراءة فقط
    internal static bool CanWriteToDirectory(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".halawa-hub-write-test-{Guid.NewGuid():N}");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
