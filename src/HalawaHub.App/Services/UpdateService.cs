using System;
using System.Threading.Tasks;
using HalawaHub.Core;
using HalawaHub.Core.Updates;

namespace HalawaHub.App.Services;

/// <summary>
/// تنفيذ IUpdateService — منطق التحديث الذاتي.
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly UpdateChecker _updateChecker;

    public UpdateService(UpdateChecker updateChecker)
    {
        _updateChecker = updateChecker;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        var update = await _updateChecker.CheckForUpdateAsync();

        if (update is not { IsNewer: true })
        {
            return new UpdateCheckResult(false, null, null, null, "البرنامج محدّث لآخر إصدار.");
        }

        Log.Info("تحديث جديد متوفر: v" + update.LatestVersion);
        var message = $"يتوفر إصدار جديد: v{update.LatestVersion} (لديك v{AppInfo.Version})";

        return new UpdateCheckResult(true, update.LatestVersion, update.DownloadUrl, update.Sha256, message);
    }

    public async Task<UpdateInstallResult> InstallUpdateAsync(
        string downloadUrl,
        string? sha256,
        Action<string>? onStatus)
    {
        var success = await SelfUpdater.DownloadAndApplyAsync(downloadUrl, onStatus, sha256);

        if (success)
        {
            return new UpdateInstallResult(true, "التحديث جاهز، البرنامج بيعيد التشغيل الآن...");
        }

        Log.Error("فشل التحديث التلقائي");
        return new UpdateInstallResult(false, "فشل التحديث التلقائي. جرّب لاحقًا أو حمّل من صفحة الإصدارات على GitHub يدويًا.");
    }
}
