using System;
using System.Threading.Tasks;

namespace HalawaHub.App.Services;

/// <summary>
/// نتيجة فحص التحديث.
/// </summary>
public record UpdateCheckResult(
    bool HasUpdate,
    string? LatestVersion,
    string? DownloadUrl,
    string? Sha256,
    string Message);

/// <summary>
/// نتيجة تثبيت التحديث.
/// </summary>
public record UpdateInstallResult(bool Success, string Message);

/// <summary>
/// خدمة التحديث الذاتي — منطق نقي بدون UI state.
/// </summary>
public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync();

    Task<UpdateInstallResult> InstallUpdateAsync(
        string downloadUrl,
        string? sha256,
        Action<string>? onStatus);
}
