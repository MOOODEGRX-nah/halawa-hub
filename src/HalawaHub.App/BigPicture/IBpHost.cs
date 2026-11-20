using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HalawaHub.App.BigPicture;

/// <summary>نتيجة التشغيل: نجاح، أو فشل مع السبب الحقيقي (من فحص ما قبل التشغيل).</summary>
public sealed record BpLaunchResult(bool Ok, string Reason = "", string Detail = "", bool CanOpenLauncher = false)
{
    public static BpLaunchResult Success { get; } = new(true);
}

/// <summary>إعدادات الشاشة الكبيرة (تُحفظ في config.json).</summary>
public sealed class BpSettings
{
    public bool OpenOnController { get; set; } = true;
    public bool StartInBigPicture { get; set; }
    public bool ExitOnDisconnect { get; set; } = true;
    /// auto, xbox, ps
    public string Glyphs { get; set; } = "auto";
    public bool SwapConfirmBack { get; set; }
    /// normal, large
    public string TextSize { get; set; } = "normal";
    public bool ShowClock { get; set; } = true;
}

/// <summary>ما تحتاجه الشاشة الكبيرة من البرنامج. الاختبارات تمرر نسخة وهمية.</summary>
public interface IBpHost
{
    IReadOnlyList<BpGame> Games { get; }
    BpSettings Settings { get; }
    void SaveSettings();
    void SetLanguage(string language);

    void ToggleFavorite(BpGame game);
    Task<BpLaunchResult> LaunchAsync(BpGame game, CancellationToken token);
    void OpenUpdateInLauncher(BpGame game);
    void OpenLauncher(BpGame game);
    void OpenGameSettings(BpGame game);
    void OpenNews(BpGame game, int index);

    IReadOnlyList<BpNotification> Notifications { get; }
    void DismissNotification(BpNotification item);
    void ClearNotifications();
    void MarkNotificationsRead();

    void ExitBigPicture();
    void CloseApp();
}
