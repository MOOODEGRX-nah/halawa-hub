using System;
using System.Threading.Tasks;

namespace HalawaHub.App.Services;

/// <summary>
/// نتيجة فحص التحديث.
/// </summary>
/// <param name="Failed">true يعني أن الفحص نفسه فشل، فلا نعرف هل يوجد تحديث أم لا.</param>
/// <param name="IsReleasePage">DownloadUrl صفحة إصدار تُفتح في المتصفح، وليس ملفًا للتثبيت.</param>
public record UpdateCheckResult(
    bool HasUpdate,
    string? LatestVersion,
    string? DownloadUrl,
    string? Sha256,
    string Message,
    bool Failed = false,
    bool IsReleasePage = false);

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
