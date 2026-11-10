using System.Text;

namespace HalawaHub.Core;

/// <summary>
/// كتابة آمنة لملفات بيانات المستخدم: ملف مؤقت ثم استبدال، مع نسخة .bak من
/// المحتوى السابق. انقطاع الكهرباء أو امتلاء القرص أثناء الكتابة لا يترك ملفًا
/// نصف مكتوب، والقراءة ترجع إلى .bak إذا كان الملف الأصلي تالفًا.
/// </summary>
public static class AtomicFile
{
    public static string BackupPathFor(string path) => path + ".bak";

    /// يرمي الاستثناء للمستدعي ليقرر (عادة: يسجّله ويكمل)
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        try
        {
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = new UTF8Encoding(false).GetBytes(content);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, BackupPathFor(path), ignoreMetadataErrors: true);
                }
                catch (IOException)
                {
                    // File.Replace يفشل أحيانًا إذا كان برنامج حماية يفتح الملف؛ نفس النتيجة بخطوتين
                    File.Copy(path, BackupPathFor(path), overwrite: true);
                    File.Move(tmp, path, overwrite: true);
                }
            }
            else
            {
                File.Move(tmp, path);
            }
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// يقرأ الملف ويحلله؛ إن كان مفقودًا يرجع null، وإن كان تالفًا يجرّب .bak.
    /// كل فشل يُسجَّل في السجل بدل أن يُبتلع بصمت.
    /// </summary>
    public static T? ReadWithBackup<T>(string path, Func<string, T?> parse, string what) where T : class
    {
        if (TryRead(path, parse, what, out var value)) return value;

        var bak = BackupPathFor(path);
        if (File.Exists(bak) && TryRead(bak, parse, what + " (.bak)", out var fromBak))
        {
            Log.Info($"{what}: restored from backup");
            return fromBak;
        }
        return null;
    }

    private static bool TryRead<T>(string path, Func<string, T?> parse, string what, out T? value) where T : class
    {
        value = null;
        if (!File.Exists(path)) return false;
        try
        {
            value = parse(File.ReadAllText(path));
            if (value != null) return true;
            Log.Error($"{what}: file is empty or null: {path}");
        }
        catch (Exception ex)
        {
            Log.Error($"{what}: unreadable file {path}", ex);
        }
        return false;
    }
}
