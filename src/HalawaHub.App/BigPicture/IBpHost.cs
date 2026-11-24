using System;
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
    /// الخلفية (7): grid الافتراضية، blobs، aurora، grain، dots، flow، contour، stars، spot، plain
    public string Wallpaper { get; set; } = "grid";
    /// move (عند التنقل، الافتراضي)، always، still
    public string WallpaperMotion { get; set; } = "move";
    /// «تتفاعل مع التنقل»
    public bool WallpaperReact { get; set; } = true;
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
    /// نافذة اللعبة العادية (الاسم والغلاف وخيارات التشغيل)؛ تعود الشاشة الكبيرة على اللعبة نفسها بعد إغلاقها
    void OpenGameSettings(BpGame game);
    /// إخفاء من المكتبة، ثم إعادة قراءة الألعاب
    void HideGame(BpGame game);
    void OpenNews(BpGame game, string url);
    /// أخبار Steam للعبة (العنوان، التاريخ، الرابط)؛ فارغة لغير Steam
    Task<IReadOnlyList<(string Title, string Date, string Url)>> LoadNewsAsync(BpGame game);

    /// إشعار جديد وصل (يُستدعى على خيط الواجهة)
    event Action<BpNotification>? NotificationArrived;
    /// تغيّرت البيانات: true = المكتبة نفسها (فحص جديد)، false = تفصيل (حجم اللعبة وصل)
    event Action<bool>? DataChanged;
    IReadOnlyList<BpNotification> Notifications { get; }
    void DismissNotification(BpNotification item);
    void ClearNotifications();
    void MarkNotificationsRead();

    // ===== العروض (تبويب العروض) =====
    /// صفوف العروض بالترتيب: مجانيات Epic، تخفيضات Steam، متاجر أخرى
    IReadOnlyList<BpDealSource> Deals => Array.Empty<BpDealSource>();
    /// «آخر تحديث قبل N دقيقة · يتحدّث كل ساعة»، أو فارغ
    string DealsUpdatedText => "";
    void OpenUrl(string url) { }

    // ===== العمود الجانبي في الرئيسي وصفحة اللعبة =====
    /// لقطات اللعبة (متجر Steam)، أو فارغة
    Task<IReadOnlyList<string>> LoadScreenshotsAsync(BpGame game) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    /// ساعات آخر 7 أيام (آخرها اليوم) كما سجلها Halawa
    double[] WeekHours(BpGame game) => new double[7];
    BpActivity? Activity(BpGame game) => null;

    // ===== إعدادات اللعبة داخل الشاشة الكبيرة =====
    BpGameInfo GameInfo(BpGame game) => new() { Name = game.Name, OriginalName = game.Name };
    void RenameGame(BpGame game, string name) { }
    IReadOnlyList<BpCollectionToggle> GameCollections(BpGame game) => Array.Empty<BpCollectionToggle>();
    void SetCollection(BpGame game, string collectionId, bool member) { }
    void OpenFileLocation(BpGame game) { }
    void VerifyFiles(BpGame game) { }
    bool CreateShortcut(BpGame game) => false;
    void Uninstall(BpGame game) { }
    void SetLaunchArgs(BpGame game, string args) { }
    void RestoreCover(BpGame game) { }
    void SetOwnCover(BpGame game, string path) { }
    /// متصفح SteamGridDB المشترك مع النافذة العادية
    HalawaHub.App.ViewModels.SgdbBrowser? Sgdb => null;
    bool HasSgdbKey => false;
    string SgdbKeyUrl => "https://www.steamgriddb.com/profile/preferences/api";
    /// يتحقق من المفتاح ويحفظه
    Task<bool> SaveSgdbKeyAsync(string key) => Task.FromResult(false);
    /// ينزّل الصورة المختارة ويستعملها حسب نوعها
    Task<bool> PickSgdbAsync(BpGame game, HalawaHub.App.ViewModels.SgdbTile tile) => Task.FromResult(false);

    void ExitBigPicture();
    void CloseApp();
}
