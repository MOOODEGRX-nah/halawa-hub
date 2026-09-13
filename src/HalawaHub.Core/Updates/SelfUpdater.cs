using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HalawaHub.Core;

namespace HalawaHub.Core.Updates;

public static class SelfUpdater
{
    public static async Task<bool> DownloadAndApplyAsync(string downloadUrl, Action<string>? onStatus = null, string? expectedSha256 = null)
    {
        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "HalawaHub-Update");
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            Directory.CreateDirectory(tempRoot);

            var zipPath = Path.Combine(tempRoot, "update.zip");
            var extractPath = Path.Combine(tempRoot, "extracted");

            onStatus?.Invoke("جاري تحميل التحديث...");
            Log.Info($"بدء تحميل التحديث من: {downloadUrl}");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HalawaHub-Updater/1.0");
                var bytes = await http.GetByteArrayAsync(downloadUrl);

                // تحقق من سلامة الملف قبل فك الضغط (حماية من تلف أو تلاعب)
                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Error($"فشل تحقق SHA256: متوقع {expectedSha256} فعلي {actual}");
                        onStatus?.Invoke("فشل التحقق من سلامة ملف التحديث (SHA256) — أُلغي التثبيت.");
                        return false;
                    }
                    Log.Info("تحقق SHA256 للتحديث نجح");
                }

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
            Log.Info("إعادة التشغيل لتحديث البرنامج");
            Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("فشل تحديث البرنامج", ex);
            return false;
        }
    }
}
