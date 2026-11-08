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

        if (update is null)
        {
            return new UpdateCheckResult(false, null, null, null,
                Loc.Instance["updCheckFailed"], Failed: true);
        }

        if (!update.IsNewer)
        {
            return new UpdateCheckResult(false, null, null, null, Loc.Instance["updUpToDate"]);
        }

        Log.Info("تحديث جديد متوفر: v" + update.LatestVersion);
        var message = Loc.Instance.Format("updAvailable", update.LatestVersion, AppInfo.Version);

        return new UpdateCheckResult(true, update.LatestVersion, update.DownloadUrl, update.Sha256, message,
            IsReleasePage: update.IsReleasePage);
    }

    // SelfUpdater في Core لا يعرف الترجمة: رسائله ثابتة نترجمها هنا
    internal static string LocalizeStatus(string status) => status switch
    {
        "downloading update..." => Loc.Instance["updDownloading"],
        "extracting files..." => Loc.Instance["updExtracting"],
        "restarting..." => Loc.Instance["updRestarting"],
        "SHA256 verification failed - install aborted." => Loc.Instance["updChecksum"],
        "automatic update is only available on Windows." => Loc.Instance["updUnsupported"],
        _ => status
    };

    public async Task<UpdateInstallResult> InstallUpdateAsync(
        string downloadUrl,
        string? sha256,
        Action<string>? onStatus)
    {
        var success = await SelfUpdater.DownloadAndApplyAsync(
            downloadUrl, status => onStatus?.Invoke(LocalizeStatus(status)), sha256);

        if (success)
        {
            return new UpdateInstallResult(true, Loc.Instance["updReady"]);
        }

        Log.Error("فشل التحديث التلقائي");
        return new UpdateInstallResult(false, Loc.Instance["updInstallFailed"]);
    }
}
