using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using HalawaHub.Core;

namespace HalawaHub.Core.Updates;

public static class SelfUpdater
{
    // رسائل ثابتة تترجمها طبقة الواجهة (UpdateService.LocalizeStatus)
    public const string StatusDownloading = "downloading update...";
    public const string StatusExtracting = "extracting files...";
    public const string StatusRestarting = "restarting...";
    public const string StatusChecksumFailed = "SHA256 verification failed - install aborted.";
    public const string StatusChecksumMissing = "no SHA256 for this update - install refused.";

    /// هاش SHA256 صالح: 64 حرفًا ست عشريًا. بدونه لا نثبّت شيئًا على أي نظام.
    internal static bool IsValidSha256(string? sha) =>
        sha is { Length: 64 } && sha.All(Uri.IsHexDigit);
    public const string StatusUnsupported = "automatic update is only available on Windows.";

    /// <summary>وسيط تمرره نسخة لينكس القديمة للجديدة بعد الاستبدال.</summary>
    public const string WaitForPidArgument = LinuxSelfUpdater.WaitForPidArgument;

    /// <summary>حذف النسخة القديمة بعد تحديث لينكس، تستدعيها النسخة الجديدة.</summary>
    public static void CleanLinuxLeftovers() => LinuxSelfUpdater.CleanLeftovers();

    public static async Task<bool> DownloadAndApplyAsync(string downloadUrl, Action<string>? onStatus = null, string? expectedSha256 = null)
    {
        if (OperatingSystem.IsLinux())
        {
            var kind = InstallInfo.Current;
            if (kind is InstallKind.LinuxTarball or InstallKind.LinuxAppImage)
                return await LinuxSelfUpdater.ApplyAsync(kind, downloadUrl, expectedSha256, onStatus);
        }

        // آلية الاستبدال أدناه ملف .bat، فهي تعمل على ويندوز فقط
        if (!OperatingSystem.IsWindows())
        {
            Log.Error("self-update is not available for this installation");
            onStatus?.Invoke(StatusUnsupported);
            return false;
        }

        // ويندوز كان يثبّت بلا تحقق إذا غاب الهاش. لم يعد: لا تثبيت بلا SHA256 صالح
        if (!IsValidSha256(expectedSha256))
        {
            Log.Error("windows update refused: no checksum for the downloaded file");
            onStatus?.Invoke(StatusChecksumMissing);
            return false;
        }

        try
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "HalawaHub-Update");
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
            Directory.CreateDirectory(tempRoot);

            var zipPath = Path.Combine(tempRoot, "update.zip");
            var extractPath = Path.Combine(tempRoot, "extracted");

            onStatus?.Invoke(StatusDownloading);
            Log.Info($"download start from: {downloadUrl}");

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("HalawaHub-Updater/1.0");
                var bytes = await http.GetByteArrayAsync(downloadUrl);

                // الهاش مضمون هنا (فُحص أعلاه)
                {
                    var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.Error($"SHA256 mismatch: expected {expectedSha256} actual {actual}");
                        onStatus?.Invoke(StatusChecksumFailed);
                        return false;
                    }
                    Log.Info("SHA256 verification succeeded");
                }

                await File.WriteAllBytesAsync(zipPath, bytes);
            }

            onStatus?.Invoke(StatusExtracting);
            ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true);

            var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            var exeName = Path.GetFileName(Environment.ProcessPath ?? "Halawa-Hub.exe");
            var logPath = Path.Combine(tempRoot, "update.log");

            var scriptPath = Path.Combine(tempRoot, "apply-update.bat");
            var scriptContent = BuildApplyScript(logPath, exeName, extractPath, installDir);

            await File.WriteAllTextAsync(scriptPath, scriptContent);

            onStatus?.Invoke(StatusRestarting);
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

    /// <summary>bat script محسّن: taskkill + logging + retry. internal للاختبار.</summary>
    internal static string BuildApplyScript(string logPath, string exeName, string extractPath, string installDir) =>
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
        $"echo Update completed >> \"{logPath}\"\r\n" +
        "del \"%~f0\"\r\n";
}
