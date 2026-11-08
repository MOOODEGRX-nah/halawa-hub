using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Media;

namespace HalawaHub.App.Services;

/// <summary>
/// نصوص الواجهة بلغتين. نصوص التصميم منسوخة حرفيًا من T.ar / T.en في ملفات
/// design-source، وما يليها نصوص البرنامج التي لا يغطيها التصميم.
/// تغيير اللغة يرفع PropertyChanged("Item[]") فتتحدث كل الروابط فورًا.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    public const string Arabic = "ar";
    public const string English = "en";

    private static readonly FontFamily ArabicFont =
        new("fonts:Inter#Inter, avares://Halawa-Hub/Assets/Fonts#Cairo");
    private static readonly FontFamily EnglishFont = new("fonts:Inter#Inter");

    private string _language = Arabic;

    public string Language
    {
        get => _language;
        set
        {
            var normalized = value == English ? English : Arabic;
            if (normalized == _language) return;
            _language = normalized;
            // روابط الفهرس في Avalonia تستمع إلى "Item"، والاسم القياسي في .NET هو "Item[]"
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRtl)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontFamily)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsRtl => _language == Arabic;
    public Avalonia.Media.FlowDirection FlowDirection =>
        IsRtl ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
    public FontFamily FontFamily => IsRtl ? ArabicFont : EnglishFont;

    public string this[string key]
    {
        get
        {
            var table = _language == English ? En : Ar;
            return table.TryGetValue(key, out var value) ? value : key;
        }
    }

    public string Format(string key, params object[] args) => string.Format(this[key], args);

    public event EventHandler? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal static IReadOnlyDictionary<string, string> ArabicStrings => Ar;
    internal static IReadOnlyDictionary<string, string> EnglishStrings => En;

    private static readonly Dictionary<string, string> Ar = new()
    {
        // ===== التصميم: Screen.dc.html =====
        ["library"] = "المكتبة", ["developer"] = "المطورين", ["settings"] = "الإعدادات", ["soon"] = "قريباً",
        ["home"] = "الرئيسي", ["favorites"] = "المفضل", ["installed"] = "المثبتة", ["recent"] = "المضاف حديثًا",
        ["search"] = "ابحث في المكتبة", ["close"] = "إغلاق", ["saved"] = "تُحفظ التغييرات تلقائيًا",
        ["recentLbl"] = "الأخيرة", ["clear"] = "مسح البحث", ["tryOther"] = "جرّب كلمة أخرى",
        ["emptyPage"] = "لا يوجد شيء هنا بعد", ["refresh"] = "تحديث المكتبة", ["all"] = "كل المنصات",
        ["play"] = "تشغيل", ["addFav"] = "إضافة للمفضلة", ["remFav"] = "إزالة من المفضلة",
        ["gameSettings"] = "الإعدادات", ["more"] = "خيارات إضافية", ["copyPath"] = "نسخ المسار",
        ["soonTitle"] = "قريبًا", ["soonText"] = "نجهّز صفحة المطورين قريبًا.", ["inDev"] = "قيد التطوير...",
        ["back"] = "العودة إلى المكتبة", ["setTitle"] = "الإعدادات",
        ["sUpdates"] = "التحديثات", ["sLanguage"] = "اللغة", ["sApi"] = "مفاتيح API", ["sAppearance"] = "المظهر",
        ["sStartup"] = "التشغيل", ["sBuild"] = "معلومات النسخة",
        ["curVersion"] = "الإصدار الحالي", ["latest"] = "أنت على آخر إصدار", ["check"] = "تحقق من التحديثات",
        ["installNote"] = "يظهر زر \"تثبيت الآن\" عند وجود إصدار أحدث.",
        ["langNote"] = "يتغير اتجاه الواجهة تلقائيًا مع اللغة.",
        ["sgdb"] = "SteamGridDB (أغلفة GOG/Epic/Riot/Xbox)", ["keyPh"] = "الصق المفتاح هنا...",
        ["verify"] = "تحقق", ["getKey"] = "احصل على مفتاح", ["keyOk"] = "المفتاح صالح", ["keyBad"] = "المفتاح غير صالح",
        ["updFail"] = "تعذّر التحقق من التحديثات — تحقق من اتصالك", ["verifying"] = "جارٍ التحقق…",
        ["checking"] = "جارٍ البحث عن تحديثات…", ["moreThemes"] = "خيارات مظاهر إضافية — قريبًا",
        ["startWith"] = "تشغيل مع بدء النظام", ["startNote"] = "يفتح Halawa-Hub تلقائيًا عند بدء تشغيل النظام",
        ["version"] = "الإصدار", ["repo"] = "المستودع", ["openGh"] = "فتح GitHub",

        // ===== التصميم: GameDialog.dc.html =====
        ["general"] = "عام", ["files"] = "الملفات المثبتة", ["custom"] = "التخصيص", ["launch"] = "خيارات التشغيل",
        ["gameName"] = "اسم اللعبة", ["nameNote"] = "يظهر في المكتبة فقط ولا يغيّر ملفات اللعبة.",
        ["platform"] = "المنصة", ["hide"] = "إخفاء من المكتبة", ["storage"] = "مكان التخزين",
        ["openLoc"] = "فتح مكان الملفات", ["sizeOnDisk"] = "الحجم على القرص", ["changeImg"] = "تغيير الصورة",
        ["searchSgdb"] = "بحث في SteamGridDB", ["restore"] = "استعادة الصورة الأصلية",
        ["imgNote"] = "صورة عمودية بنسبة 2:3 تعطي أفضل نتيجة (PNG أو JPG).",
        ["launchNote"] = "للمستخدمين المتقدمين: معاملات تُضاف عند تشغيل اللعبة.",

        // ===== نصوص البرنامج غير الموجودة في التصميم =====
        ["noResults"] = "لا توجد نتائج لـ «{0}»",
        ["sortBy"] = "ترتيب", ["sortName"] = "الاسم", ["sortLastPlayed"] = "آخر تشغيل", ["sortPlatform"] = "المنصة",
        ["tools"] = "الأدوات", ["toolApply"] = "تطبيق", ["toolRemove"] = "إزالة", ["uninstall"] = "إلغاء التثبيت",
        ["statusLabel"] = "الحالة", ["statusInstalled"] = "مثبتة", ["statusNotInstalled"] = "غير مثبتة",
        ["playTime"] = "وقت اللعب", ["playedUnderMinute"] = "أقل من دقيقة", ["playedMinutes"] = "{0} دقيقة",
        ["playedHours"] = "{0} ساعة", ["playedLots"] = "+100 ساعة",
        ["lastPlayedMinutes"] = "قبل دقائق", ["lastPlayedHours"] = "قبل {0} ساعة", ["lastPlayedYesterday"] = "أمس",
        ["lastPlayedDays"] = "قبل {0} أيام", ["lastPlayedWeeks"] = "قبل {0} أسبوع",
        ["launchFailed"] = "تعذر تشغيل {0}: {1}", ["scanFailed"] = "تعذر فحص المكتبات: {0}",
        ["uninstallFailed"] = "تعذر فتح نافذة إلغاء التثبيت: {0}",
        ["uninstallUnsupported"] = "إلغاء التثبيت غير مدعوم بعد لهذه المنصة. احذفها من مشغّل المنصة نفسه.",
        ["openLocFailed"] = "تعذر فتح مكان الملفات: {0}", ["launchPh"] = "مثال: -novid -dx11",
        ["updAvailable"] = "يتوفر إصدار جديد: v{0} (لديك v{1})", ["updUpToDate"] = "البرنامج محدّث لآخر إصدار.",
        ["updCheckFailed"] = "تعذّر التحقق من التحديثات. تأكد من اتصالك بالإنترنت وحاول مرة أخرى.",
        ["install"] = "تثبيت الآن", ["openRelease"] = "فتح صفحة الإصدار",
        ["updDownloading"] = "جاري تحميل التحديث...", ["updExtracting"] = "جاري فك الملفات...",
        ["updRestarting"] = "جاري إعادة التشغيل...", ["updReady"] = "التحديث جاهز، البرنامج يعيد التشغيل الآن...",
        ["updChecksum"] = "فشل التحقق من سلامة الملف، أُلغي التثبيت.",
        ["updUnsupported"] = "التحديث التلقائي غير متاح لهذا النوع من التثبيت.",
        ["updInstallFailed"] = "فشل التحديث التلقائي. جرّب لاحقًا أو حمّل من صفحة الإصدارات على GitHub يدويًا.",
        ["whatsNew"] = "ما الجديد", ["whatsNewClose"] = "حسنًا",
        ["trayShow"] = "إظهار Halawa-Hub", ["trayExit"] = "خروج", ["chooseCover"] = "اختر صورة الغلاف",
        ["showHidden"] = "إظهار الألعاب المخفية", ["showHiddenNote"] = "تعود الألعاب التي أخفيتها للظهور في كل الصفحات.",
        ["startUnsupported"] = "هذه الميزة غير متاحة على هذا النظام",
        ["appMenu"] = "إظهار في قائمة التطبيقات", ["appMenuNote"] = "يضيف Halawa-Hub وأيقونته إلى قائمة تطبيقات النظام",
        ["launchUrlNote"] = "المشغّل يبدأ هذه اللعبة بنفسه، فالمعاملات تُضبط من إعدادات اللعبة في المشغّل",
        ["sgdbNeedKey"] = "أضف مفتاح SteamGridDB من الإعدادات أولًا.", ["sgdbSearching"] = "جارٍ البحث…",
        ["sgdbNone"] = "لم نجد صورًا لهذه اللعبة.", ["sgdbPick"] = "اختر صورة", ["sgdbBack"] = "رجوع",
        ["sgdbFailed"] = "تعذر تنزيل الصورة.",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        // ===== Design: Screen.dc.html =====
        ["library"] = "Library", ["developer"] = "Developer", ["settings"] = "Settings", ["soon"] = "Coming soon",
        ["home"] = "Home", ["favorites"] = "Favorites", ["installed"] = "Installed", ["recent"] = "Recently Added",
        ["search"] = "Search library", ["close"] = "Close", ["saved"] = "Changes are saved automatically",
        ["recentLbl"] = "Recent", ["clear"] = "Clear search", ["tryOther"] = "Try a different word",
        ["emptyPage"] = "Nothing here yet", ["refresh"] = "Refresh library", ["all"] = "All platforms",
        ["play"] = "Play", ["addFav"] = "Add to favorites", ["remFav"] = "Remove from favorites",
        ["gameSettings"] = "Settings", ["more"] = "More options", ["copyPath"] = "Copy path",
        ["soonTitle"] = "Coming soon", ["soonText"] = "We are preparing the Developer page. Coming soon.",
        ["inDev"] = "In development...", ["back"] = "Back to Library", ["setTitle"] = "Settings",
        ["sUpdates"] = "Updates", ["sLanguage"] = "Language", ["sApi"] = "API keys", ["sAppearance"] = "Appearance",
        ["sStartup"] = "Startup", ["sBuild"] = "Build info",
        ["curVersion"] = "Current version", ["latest"] = "You are on the latest version", ["check"] = "Check for updates",
        ["installNote"] = "\"Install now\" appears when a newer version exists.",
        ["langNote"] = "The interface direction follows the language.",
        ["sgdb"] = "SteamGridDB (GOG/Epic/Riot/Xbox covers)", ["keyPh"] = "Paste key here...",
        ["verify"] = "Verify", ["getKey"] = "Get a key", ["keyOk"] = "Key is valid", ["keyBad"] = "Key is invalid",
        ["updFail"] = "Couldn't check for updates — check your connection", ["verifying"] = "Verifying…",
        ["checking"] = "Checking for updates…", ["moreThemes"] = "More themes — coming soon",
        ["startWith"] = "Start with system", ["startNote"] = "Opens Halawa-Hub automatically when the system starts",
        ["version"] = "Version", ["repo"] = "Repository", ["openGh"] = "Open GitHub",

        // ===== Design: GameDialog.dc.html =====
        ["general"] = "General", ["files"] = "Installed files", ["custom"] = "Customization", ["launch"] = "Launch options",
        ["gameName"] = "Game name", ["nameNote"] = "Shown in the library only; game files are not changed.",
        ["platform"] = "Platform", ["hide"] = "Hide from library", ["storage"] = "Storage location",
        ["openLoc"] = "Open file location", ["sizeOnDisk"] = "Size on disk", ["changeImg"] = "Change image",
        ["searchSgdb"] = "Search SteamGridDB", ["restore"] = "Restore original",
        ["imgNote"] = "A 2:3 portrait image works best (PNG or JPG).",
        ["launchNote"] = "Advanced users: arguments added when the game starts.",

        // ===== App strings the design does not cover =====
        ["noResults"] = "No results for “{0}”",
        ["sortBy"] = "Sort", ["sortName"] = "Name", ["sortLastPlayed"] = "Last played", ["sortPlatform"] = "Platform",
        ["tools"] = "Tools", ["toolApply"] = "Apply", ["toolRemove"] = "Remove", ["uninstall"] = "Uninstall",
        ["statusLabel"] = "Status", ["statusInstalled"] = "Installed", ["statusNotInstalled"] = "Not installed",
        ["playTime"] = "Play time", ["playedUnderMinute"] = "Under a minute", ["playedMinutes"] = "{0} min",
        ["playedHours"] = "{0} h", ["playedLots"] = "100+ h",
        ["lastPlayedMinutes"] = "Minutes ago", ["lastPlayedHours"] = "{0} h ago", ["lastPlayedYesterday"] = "Yesterday",
        ["lastPlayedDays"] = "{0} days ago", ["lastPlayedWeeks"] = "{0} weeks ago",
        ["launchFailed"] = "Couldn't start {0}: {1}", ["scanFailed"] = "Couldn't scan libraries: {0}",
        ["uninstallFailed"] = "Couldn't open the uninstall window: {0}",
        ["uninstallUnsupported"] = "Uninstall isn't supported for this platform yet. Remove it from the platform's own launcher.",
        ["openLocFailed"] = "Couldn't open the file location: {0}", ["launchPh"] = "Example: -novid -dx11",
        ["updAvailable"] = "New version available: v{0} (you have v{1})", ["updUpToDate"] = "You are on the latest version.",
        ["updCheckFailed"] = "Couldn't check for updates. Check your internet connection and try again.",
        ["install"] = "Install now", ["openRelease"] = "Open release page",
        ["updDownloading"] = "Downloading update...", ["updExtracting"] = "Extracting files...",
        ["updRestarting"] = "Restarting...", ["updReady"] = "Update ready, restarting now...",
        ["updChecksum"] = "File verification failed, install cancelled.",
        ["updUnsupported"] = "Automatic update isn't available for this type of install.",
        ["updInstallFailed"] = "Automatic update failed. Try later or download it from the GitHub releases page.",
        ["whatsNew"] = "What's new", ["whatsNewClose"] = "Got it",
        ["trayShow"] = "Show Halawa-Hub", ["trayExit"] = "Exit", ["chooseCover"] = "Choose a cover image",
        ["showHidden"] = "Show hidden games", ["showHiddenNote"] = "Games you hid appear again on every page.",
        ["startUnsupported"] = "This feature isn't available on this system",
        ["appMenu"] = "Show in the applications menu", ["appMenuNote"] = "Adds Halawa-Hub and its icon to the system applications menu",
        ["launchUrlNote"] = "This game is started by its launcher, so set launch options in the launcher's game settings",
        ["sgdbNeedKey"] = "Add a SteamGridDB key in Settings first.", ["sgdbSearching"] = "Searching…",
        ["sgdbNone"] = "No images found for this game.", ["sgdbPick"] = "Pick an image", ["sgdbBack"] = "Back",
        ["sgdbFailed"] = "Couldn't download the image.",
    };
}
