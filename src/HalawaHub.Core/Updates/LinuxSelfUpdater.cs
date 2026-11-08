using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace HalawaHub.Core.Updates;

/// <summary>
/// التحديث الذاتي على لينكس. لينكس يسمح باستبدال ملف أو مجلد قيد التشغيل، فلا نحتاج
/// سكربت مساعد مثل ويندوز: نجهز النسخة الجديدة ونتحقق أنها تعمل، ثم نبدّلها بعملية
/// rename واحدة، ثم نشغلها وننتظر منها أن تبدأ بعد خروجنا (--wait-for-pid).
/// بيانات المستخدم في ~/.local/share/HalawaHub خارج كل المسارات هنا ولا تُلمس.
/// </summary>
internal static class LinuxSelfUpdater
{
    internal const string ExecutableName = "Halawa-Hub";
    internal const string WaitForPidArgument = "--wait-for-pid";

    private const string StagingInfix = ".update-";
    private const string BackupInfix = ".old-";

    public static async Task<bool> ApplyAsync(InstallKind kind, string downloadUrl, string? expectedSha256, Action<string>? onStatus)
    {
        // ويندوز يكمل بلا تحقق إذا غاب الهاش (للتوافق مع إصدارات قديمة). هنا لا يوجد
        // قديم نحافظ عليه، فملف لا نستطيع التحقق منه لا يستبدل برنامجًا يعمل.
        if (string.IsNullOrEmpty(expectedSha256))
        {
            Log.Error("linux update refused: no checksum for the downloaded file");
            onStatus?.Invoke(SelfUpdater.StatusChecksumFailed);
            return false;
        }

        try
        {
            var relaunchPath = kind switch
            {
                InstallKind.LinuxTarball => await UpdateTarballAsync(downloadUrl, expectedSha256, onStatus),
                InstallKind.LinuxAppImage => await UpdateAppImageAsync(downloadUrl, expectedSha256, onStatus),
                _ => null
            };
            if (relaunchPath == null) return false;

            onStatus?.Invoke(SelfUpdater.StatusRestarting);
            Log.Info($"linux update applied, restarting {relaunchPath}");
            Relaunch(relaunchPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("linux update failed", ex);
            return false;
        }
    }

    // ===== tar.gz: استبدال مجلد التثبيت =====

    private static async Task<string?> UpdateTarballAsync(string downloadUrl, string expectedSha256, Action<string>? onStatus)
    {
        var installDir = AppContext.BaseDirectory.TrimEnd('/');
        var staging = installDir + StagingInfix + Environment.ProcessId;

        CleanLeftovers(installDir);
        Directory.CreateDirectory(staging);

        try
        {
            var archive = Path.Combine(staging, "update.tar.gz");

            onStatus?.Invoke(SelfUpdater.StatusDownloading);
            if (!await DownloadVerifiedAsync(downloadUrl, archive, expectedSha256))
            {
                onStatus?.Invoke(SelfUpdater.StatusChecksumFailed);
                return null;
            }

            onStatus?.Invoke(SelfUpdater.StatusExtracting);
            var newBuild = ExtractTarball(archive, Path.Combine(staging, "extracted"));
            VerifyRuns(Path.Combine(newBuild, ExecutableName));

            KeepUserPlugins(installDir, newBuild);
            SwapDirectories(installDir, newBuild, installDir + BackupInfix + Environment.ProcessId);

            return Path.Combine(installDir, ExecutableName);
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    /// <returns>المجلد الذي يحتوي الملف التنفيذي داخل الأرشيف.</returns>
    internal static string ExtractTarball(string archivePath, string destination)
    {
        Directory.CreateDirectory(destination);

        // TarFile يرفض المسارات الخارجة عن المجلد ويحافظ على صلاحيات الملفات
        using (var file = File.OpenRead(archivePath))
        using (var gzip = new GZipStream(file, CompressionMode.Decompress))
        {
            TarFile.ExtractToDirectory(gzip, destination, overwriteFiles: false);
        }

        var root = File.Exists(Path.Combine(destination, ExecutableName))
            ? destination
            : Directory.GetDirectories(destination).FirstOrDefault(d => File.Exists(Path.Combine(d, ExecutableName)));

        if (root == null)
            throw new InvalidDataException("the archive does not contain the Halawa-Hub executable");

        MakeExecutable(Path.Combine(root, ExecutableName));
        return root;
    }

    /// مثل xcopy على ويندوز: إضافات أضافها المستخدم لا تضيع مع التحديث
    internal static void KeepUserPlugins(string installDir, string newBuild)
    {
        var oldPlugins = Path.Combine(installDir, "Plugins");
        if (!Directory.Exists(oldPlugins)) return;

        var newPlugins = Path.Combine(newBuild, "Plugins");
        Directory.CreateDirectory(newPlugins);

        foreach (var file in Directory.GetFiles(oldPlugins))
        {
            var target = Path.Combine(newPlugins, Path.GetFileName(file));
            if (!File.Exists(target)) File.Copy(file, target);
        }
    }

    /// التبديل عمليتا rename: لو فشلت الثانية نرجع النسخة القديمة فلا يبقى البرنامج بلا مجلد
    internal static void SwapDirectories(string installDir, string newBuild, string backupDir)
    {
        Directory.Move(installDir, backupDir);
        try
        {
            Directory.Move(newBuild, installDir);
        }
        catch
        {
            Directory.Move(backupDir, installDir);
            throw;
        }
    }

    /// <summary>
    /// تستدعيها النسخة الجديدة بعد التحديث. لا تُحذف النسخة القديمة أثناء عملها،
    /// فقد تحتاج تحميل مكتبة من مجلدها حتى لحظة الخروج.
    /// </summary>
    public static void CleanLeftovers() => CleanLeftovers(AppContext.BaseDirectory.TrimEnd('/'));

    internal static void CleanLeftovers(string installDir)
    {
        var parent = Path.GetDirectoryName(installDir);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent)) return;

        var name = Path.GetFileName(installDir);
        foreach (var pattern in new[] { name + StagingInfix + "*", name + BackupInfix + "*" })
        {
            foreach (var leftover in Directory.GetDirectories(parent, pattern))
                TryDeleteDirectory(leftover);
        }
    }

    // ===== AppImage: استبدال الملف =====

    private static async Task<string?> UpdateAppImageAsync(string downloadUrl, string expectedSha256, Action<string>? onStatus)
    {
        var target = InstallInfo.CurrentAppImage;
        if (string.IsNullOrEmpty(target)) return null;

        // في نفس المجلد حتى تكون إعادة التسمية عملية ذرية على نفس القرص
        var staged = target + StagingInfix + Environment.ProcessId;
        try
        {
            onStatus?.Invoke(SelfUpdater.StatusDownloading);
            if (!await DownloadVerifiedAsync(downloadUrl, staged, expectedSha256))
            {
                onStatus?.Invoke(SelfUpdater.StatusChecksumFailed);
                return null;
            }

            ReplaceAppImage(staged, target, VerifyRuns);
            return target;
        }
        finally
        {
            TryDeleteFile(staged);
        }
    }

    internal static void ReplaceAppImage(string staged, string target, Action<string> verify)
    {
        MakeExecutable(staged);
        verify(staged);
        File.Move(staged, target, overwrite: true);
    }

    // ===== مشترك =====

    internal static async Task<bool> DownloadVerifiedAsync(string url, string destination, string expectedSha256)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HalawaHub-Updater/1.0");

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        byte[] hash;
        await using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
        await using (var input = await response.Content.ReadAsStreamAsync())
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                hasher.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read));
            }
            hash = hasher.GetHashAndReset();
        }

        var actual = Convert.ToHexString(hash);
        if (string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            Log.Info("SHA256 verification succeeded");
            return true;
        }

        Log.Error($"SHA256 mismatch: expected {expectedSha256} actual {actual}");
        TryDeleteFile(destination);
        return false;
    }

    /// <summary>
    /// نشغّل النسخة الجديدة بـ --version قبل الاستبدال: ملف لا يعمل على هذا الجهاز لا يحل
    /// محل نسخة تعمل، ونسخة ليست أحدث تعني حلقة تحديث لا تنتهي (E09).
    /// </summary>
    private static void VerifyRuns(string executable)
    {
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("--version");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("could not start the new build");

        // نقرأ الخطأ أيضًا حتى لا يتوقف البرنامج الجديد على أنبوب ممتلئ
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("the new build did not answer --version in time");
        }

        var output = stdout.GetAwaiter().GetResult();
        _ = stderr.GetAwaiter().GetResult();

        if (!IsNewerBuild(process.ExitCode, output, AppInfo.Current))
            throw new InvalidDataException($"the new build is not usable: exit {process.ExitCode}, version '{output.Trim()}'");
    }

    internal static bool IsNewerBuild(int exitCode, string versionOutput, Version current) =>
        exitCode == 0 && Version.TryParse(versionOutput.Trim(), out var version) && version > current;

    private static void Relaunch(string executable)
    {
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable) ?? "/"
        };
        psi.ArgumentList.Add(WaitForPidArgument);
        psi.ArgumentList.Add(Environment.ProcessId.ToString());

        // بلا Dispose للانتظار: العملية الجديدة تعيش بعد خروجنا
        Process.Start(psi);
    }

    internal static void MakeExecutable(string path)
    {
        const UnixFileMode executeBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(path, File.GetUnixFileMode(path) | executeBits);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Error($"could not remove {path}", ex);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Error($"could not remove {path}", ex);
        }
    }
}
