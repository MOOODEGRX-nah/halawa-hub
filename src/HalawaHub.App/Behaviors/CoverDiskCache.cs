using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HalawaHub.App.Behaviors;

/// <summary>
/// كاش أغلفة على القرص: يحفظ بايتات الصورة بملف اسمه hash الرابط،
/// عشان الإقلاع الجاي ما يحتاج نت. حد أقصى 400 ملف — الأقدم يُحذف.
/// كل العمليات محاطة بـ try/catch: الكاش تحسين وليس ضرورة.
/// </summary>
public static class CoverDiskCache
{
    private static readonly object Lock = new();
    private const int MaxFiles = 400;
    private static int _writeCount;

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HalawaHub", "cache", "covers");

    public static byte[]? TryRead(string cacheKey)
    {
        try
        {
            var path = PathFor(cacheKey);
            if (path == null || !File.Exists(path)) return null;
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }
    }

    public static void TryWrite(string cacheKey, byte[] bytes)
    {
        try
        {
            lock (Lock)
            {
                var path = PathFor(cacheKey);
                if (path == null) return;
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(path, bytes);

                // تنظيف دوري: كل 50 كتابة نتحقق من العدد
                if (++_writeCount >= 50)
                {
                    _writeCount = 0;
                    Cleanup();
                }
            }
        }
        catch
        {
            // تجاهل — الكاش تحسين وليس ضرورة
        }
    }

    private static void Cleanup()
    {
        try
        {
            var files = new DirectoryInfo(Dir).GetFiles("*.cache");
            if (files.Length <= MaxFiles) return;

            var ordered = files.OrderBy(f => f.LastAccessTimeUtc).ToArray();
            for (int i = 0; i < ordered.Length - MaxFiles; i++)
            {
                try { ordered[i].Delete(); } catch { /* تجاهل */ }
            }
        }
        catch
        {
            // تجاهل
        }
    }

    private static string? PathFor(string cacheKey)
    {
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey))).ToLowerInvariant();
            return Path.Combine(Dir, hash + ".cache");
        }
        catch
        {
            return null;
        }
    }
}
