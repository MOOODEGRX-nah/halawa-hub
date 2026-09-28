using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
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

            onStatus?.Invoke("downloading update...");
            Log.Info($"download start from: {downloadUrl}");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HalawaHub-Updater/1.0");
                var bytes = await http.GetByteArrayAsync(downloadUrl);

                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Error($"SHA256 mismatch: expected {expectedSha256} actual {actual}");
                        onStatus?.Invoke("SHA256 verification failed - install aborted.");
                        return false;
                    }
                    Log.Info("SHA256 verification succeeded");
                }

                await File.WriteAllBytesAsync(zipPath, bytes);
            }

            onStatus?.Invoke("extracting files...");
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            var exeName = Path.GetFileName(Environment.ProcessPath ?? "Halawa-Hub.exe");
            var logPath = Path.Combine(tempRoot, "update.log");

            // bat script محسّن: taskkill + logging + retry
            var scriptPath = Path.Combine(tempRoot, "apply-update.bat");
            var scriptContent =
                "@echo off\r\n" +
                $"echo Update started at %date% %time% > \"{logPath}\"\r\n" +
                "timeout /t 3 /nobreak >nul\r\n" +
                $"echo Killing old process... >> \"{logPath}\"\r\n" +
                $"taskkill /F /IM \"{exeName}\" /FI \"STATUS eq RUNNING\" >> \"{logPath}\" 2>&1\r\n" +
                "timeout /t 2 /nobreak >nul\r\n" +
                $"echo Copying files from {extractPath} to {installDir} >> \"{logPath}\"\r\n" +
                $"xcopy /e /y /i \"{extractPath}\\*\" \"{installDir}\\\" >> \"{logPath}\" 2>&1\r\n" +
                $"if errorlevel 1 (\r\n" +
                $"  echo xcopy failed, retrying... >> \"{logPath}\"\r\n" +
                "  timeout /t 3 /nobreak >nul\r\n" +
                $"  xcopy /e /y /i \"{extractPath}\\*\" \"{installDir}\\\" >> \"{logPath}\" 2>&1\r\n" +
                ")\r\n" +
                $"echo Starting {exeName} >> \"{logPath}\"\r\n" +
                $"start \"\" \"{Path.Combine(installDir, exeName)}\"\r\n" +
                "echo Update completed >> \"%logPath%\"\r\n" +
                "del \"%~f0\"\r\n";

            await File.WriteAllTextAsync(scriptPath, scriptContent);

            onStatus?.Invoke("restarting...");
            Log.Info("restarting to apply update");
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
            Log.Error("update failed", ex);
            return false;
        }
    }
}
