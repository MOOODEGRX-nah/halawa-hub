using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace HalawaHub.Core.Updates;

/// <summary>
/// يحمّل الإصدار الجديد ويثبّته تلقائيًا داخل البرنامج، بدل تحويل المستخدم
/// لموقع خارجي يحمّل منه يدويًا. الفكرة: نحمّل ونفك ضغط الإصدار الجديد
/// بمجلد مؤقت، نكتب سكربت صغير (.bat) ينتظر إغلاق البرنامج الحالي (عشان
/// يفلت قفل الملف على exe)، يستبدل الملفات، يعيد فتح البرنامج، ثم يحذف
/// نفسه. بعدها البرنامج يقفل نفسه ليسمح للسكربت يكمل شغله.
/// </summary>
public static class SelfUpdater
{
    public static async Task<bool> DownloadAndApplyAsync(string downloadUrl, Action<string>? onStatus = null)
    {
        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "HalawaHub-Update");
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            Directory.CreateDirectory(tempRoot);

            var zipPath = Path.Combine(tempRoot, "update.zip");
            var extractPath = Path.Combine(tempRoot, "extracted");

            onStatus?.Invoke("جاري تحميل التحديث...");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HalawaHub-Updater/1.0");
                var bytes = await http.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(zipPath, bytes);
            }

            onStatus?.Invoke("جاري استخراج الملفات...");
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            var exeName = Path.GetFileName(Environment.ProcessPath ?? "Halawa-Hub.exe");

            var scriptPath = Path.Combine(tempRoot, "apply-update.bat");
            var scriptContent =
                "@echo off\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                $"xcopy /e /y /i \"{extractPath}\\*\" \"{installDir}\\\"\r\n" +
                $"start \"\" \"{Path.Combine(installDir, exeName)}\"\r\n" +
                "del \"%~f0\"\r\n";

            await File.WriteAllTextAsync(scriptPath, scriptContent);

            onStatus?.Invoke("جاري إعادة التشغيل...");
            Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return true;
        }
        catch
        {
            return false;
        }
    }
}
