using System;
using System.IO;
using System.Linq;
using HalawaHub.Core;

namespace HalawaHub.App.Services;

/// <summary>
/// "احذف كل بيانات Halawa" يُنفَّذ عند الإقلاع التالي: البرنامج المفتوح يكتب ملفاته عند
/// الإغلاق (الإعدادات، حجم النافذة، الجلسة)، فحذفها وهو يعمل لا يدوم. علامة wipe-pending
/// في مجلد البيانات، وProgram.Main يحذف كل ما في المجلد قبل تحميل أي خدمة.
/// يُحذف: الإعدادات، المفضلة، وقت اللعب، السجل، الأغلفة المخصصة والمخزنة، الألعاب
/// المضافة يدويًا، موافقات الإضافات، السجلات. يبقى: الألعاب وملفاتها، المشغّلات، البرنامج
/// نفسه، ومفتاح SteamGridDB في مخزن مفاتيح لينكس (يُحذف أيضًا إن أمكن).
/// </summary>
public static class DataWipe
{
    public const string MarkerName = "wipe-pending";

    public static string MarkerPath => AppPaths.InData(MarkerName);

    public static void Schedule()
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(MarkerPath, DateTime.UtcNow.ToString("O"));
    }

    /// يُستدعى من Program.Main قبل أي شيء آخر. يرجع true إذا حذف شيئًا.
    public static bool RunIfScheduled() => RunIfScheduled(AppPaths.DataDirectory);

    internal static bool RunIfScheduled(string dataDir)
    {
        var marker = Path.Combine(dataDir, MarkerName);
        if (!File.Exists(marker)) return false;

        try { HalawaHub.Core.Security.SecretStore.Delete("steamgriddb-api-key"); } catch { /* best effort */ }

        foreach (var entry in Directory.EnumerateFileSystemEntries(dataDir).ToList())
        {
            try
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                else File.Delete(entry);
            }
            catch
            {
                // ملف مقفل (سجل مفتوح من نسخة أخرى؟): نكمل الباقي
            }
        }
        return true;
    }
}

/// مسح ذاكرة الأغلفة على القرص (cache/covers)
public static class CoverDiskCacheTools
{
    public static (int Files, long Bytes) Clear()
    {
        var dir = AppPaths.InData("cache", "covers");
        if (!Directory.Exists(dir)) return (0, 0);
        int files = 0;
        long bytes = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList())
        {
            try
            {
                var len = new FileInfo(f).Length;
                File.Delete(f);
                files++;
                bytes += len;
            }
            catch (Exception ex)
            {
                Log.Error($"could not delete cached cover {f}", ex);
            }
        }
        return (files, bytes);
    }
}
