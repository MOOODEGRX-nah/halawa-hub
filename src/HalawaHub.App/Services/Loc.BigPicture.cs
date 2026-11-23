using System.Collections.Generic;

namespace HalawaHub.App.Services;

/// <summary>نصوص الشاشة الكبيرة، منسوخة من T / T2 في design-source/BigPicture.dc.html.</summary>
public sealed partial class Loc
{
    static Loc()
    {
        foreach (var (k, v) in BpAr) Ar[k] = v;
        foreach (var (k, v) in BpEn) En[k] = v;
    }

    private static readonly Dictionary<string, string> BpAr = new()
    {
        ["bigPicture"] = "الشاشة الكبيرة (التلفاز ويد التحكم)", ["bpAutoMain"] = "افتح الشاشة الكبيرة عند توصيل يد تحكم",
        ["bp"] = "الشاشة الكبيرة", ["bpHome"] = "الرئيسي", ["bpLibrary"] = "المكتبة", ["bpSearch"] = "البحث", ["bpSettings"] = "الإعدادات",
        ["bpCont"] = "تابع اللعب", ["bpFavs"] = "المفضلة", ["bpInstalled"] = "المثبتة", ["bpRecent"] = "المضاف حديثًا",
        ["bpPlay"] = "تشغيل", ["bpDetails"] = "التفاصيل", ["bpAddFav"] = "أضف للمفضلة", ["bpInFav"] = "في المفضلة",
        ["bpUpdAvail"] = "تحديث متوفر",
        ["bpNever"] = "لم تُشغَّل بعد", ["bpToday"] = "اليوم", ["bpYesterday"] = "أمس",
        ["bpLongAgo"] = "قبل أكثر من سنة",
        ["bpLastLong"] = "آخر تشغيل {0}",
        ["bpNtTitle"] = "الإشعارات", ["bpNtClear"] = "امسح الكل", ["bpNtDismiss"] = "أخفِ",
        ["bpNtEmpty"] = "لا جديد، أنت مطّلع على كل شيء", ["bpNtEmptySub"] = "تظهر الإشعارات الجديدة هنا.", ["bpNtShow"] = "عرض",
        ["bpSeeAll"] = "عرض الكل", ["bpAll"] = "الكل", ["bpSelect"] = "اختيار", ["bpOpen"] = "فتح", ["bpBack"] = "رجوع", ["bpClose"] = "إغلاق",
        ["bpGuide"] = "القائمة", ["bpFilter"] = "المنصة", ["bpType"] = "كتابة", ["bpDel"] = "حذف", ["bpSpace"] = "مسافة", ["bpDone"] = "تم",
        ["bpResults"] = "النتائج", ["bpTryOther"] = "جرّب كلمة أخرى", ["bpNoResFor"] = "لا توجد نتائج لـ «{0}»", ["bpSearchPh"] = "ابحث في مكتبتك…",
        ["bpUpdTitle"] = "يوجد تحديث لـ {0}",
        ["bpUpdText"] = "ينزّله Steam قبل أن تبدأ اللعبة. حدّثها الآن من Steam، أو جرّب اللعب بالنسخة الحالية إن سمح Steam بذلك.",
        ["bpUpdNow"] = "حدّث في Steam", ["bpPlayAnyway"] = "العب على أي حال", ["bpCancel"] = "إلغاء",
        ["bpStarting"] = "جارٍ تشغيل {0}", ["bpLaunched"] = "{0} تعمل الآن", ["bpViaPlat"] = "عن طريق {0}. يبدأ احتساب وقت اللعب عند فتح اللعبة.",
        ["bpWaitPlat"] = "ننتظر أن يفتح {0} اللعبة…", ["bpFailTitle"] = "تعذّر تشغيل {0}",
        ["bpOpenLauncher"] = "افتح المشغّل", ["bpOpenLauncherOf"] = "افتح {0}", ["bpLauncherOpening"] = "جارٍ فتح {0}", ["bpRetry"] = "حاول مجددًا",
        ["bpOpenedSteam"] = "فُتحت {0} في Steam", ["bpNewsOpened"] = "يُفتح الخبر في Steam",
        ["bpResume"] = "متابعة", ["bpNotifications"] = "الإشعارات", ["bpExitBp"] = "الخروج من الشاشة الكبيرة", ["bpCloseApp"] = "أغلق Halawa",
        ["bpExiting"] = "العودة إلى النافذة العادية", ["bpGameSettings"] = "إعدادات اللعبة",
        ["bpPlayTime"] = "وقت اللعب", ["bpLastPlayed"] = "آخر تشغيل", ["bpSize"] = "الحجم على القرص", ["bpPlatform"] = "المنصة", ["bpNews"] = "الأخبار",
        ["bpNoNews"] = "لا أخبار لهذه اللعبة الآن.",
        ["bpSecGeneral"] = "عام", ["bpSecController"] = "يد التحكم", ["bpSecDisplay"] = "العرض", ["bpSecLanguage"] = "اللغة", ["bpSecExit"] = "خروج",
        ["bpAutoBp"] = "افتح الشاشة الكبيرة عند توصيل يد تحكم", ["bpAutoBpN"] = "تعمل مع Xbox وPlayStation وأغلب أيادي التحكم التي يتعرّف عليها ويندوز ولينكس.",
        ["bpStartBp"] = "ابدأ Halawa بالشاشة الكبيرة", ["bpStartBpN"] = "يفتح بملء الشاشة بدل النافذة العادية.",
        ["bpExitDisc"] = "اخرج من الشاشة الكبيرة عند فصل يد التحكم", ["bpExitDiscN"] = "يعود للنافذة العادية بعد ثوانٍ.",
        ["bpGlyphs"] = "أيقونات الأزرار", ["bpGAuto"] = "تلقائي", ["bpGlyphsN"] = "«تلقائي» يطابق يد التحكم الموصولة.",
        ["bpSwapAB"] = "بدّل زرّي التأكيد والرجوع", ["bpSwapABN"] = "لأيادي التحكم بنمط Nintendo.",
        ["bpTextSize"] = "حجم النص", ["bpTNormal"] = "عادي", ["bpTLarge"] = "كبير", ["bpTextSizeN"] = "الكبير أسهل للقراءة من بعيد.",
        ["bpShowClock"] = "أظهر الساعة", ["bpShowClockN"] = "في الشريط العلوي والقائمة.",
        ["bpMotionInfo"] = "الحركات تتبع «تقليل الحركة» في الإعدادات › المظهر.",
        ["bpLangNote"] = "يتغير اتجاه الواجهة مع اللغة.",
        ["bpExitRow"] = "الخروج من الشاشة الكبيرة", ["bpExitRowN"] = "تُغلق الشاشة الكبيرة وتفتح نافذة Halawa العادية. يد التحكم تبقى تعمل في المكتبة.",
        ["bpNtUpdT"] = "تحديث متوفر لـ {0}", ["bpNtUpdB"] = "سيُنزّل Steam التحديث قبل أن تبدأ اللعبة.", ["bpNtScanT"] = "انتهى الفحص",
        ["bpNtAppT"] = "إصدار جديد من Halawa Hub", ["bpNtAppB"] = "افتح النافذة العادية ثم الإعدادات › التحديثات لتثبيته.", ["bpNtFreeT"] = "مجانية هذا الأسبوع على Epic",
        ["bpNow"] = "الآن",
        ["bpWhyDrive"] = "القرص غير موصول", ["bpWhyFiles"] = "ملفات اللعبة غير موجودة", ["bpWhyLauncher"] = "المشغّل غير مثبت", ["bpWhySteam"] = "لم تُسجّل الدخول إلى Steam",
        ["bpController"] = "يد التحكم موصولة", ["bpNoController"] = "لوحة المفاتيح", ["bpSaved"] = "تُحفظ التغييرات تلقائيًا",
        ["bpHideGame"] = "أخفِ من المكتبة", ["bpHideGameN"] = "تعيدها من الإعدادات › المكتبة › إظهار الألعاب المخفية في النافذة العادية.",
        ["bpMoreSettings"] = "إعدادات أكثر في النافذة العادية", ["bpMoreSettingsN"] = "الاسم والغلاف وخيارات التشغيل. تعود الشاشة الكبيرة عند إغلاقها.",
        ["bpHiddenToast"] = "أُخفيت {0} من المكتبة",
    };

    private static readonly Dictionary<string, string> BpEn = new()
    {
        ["bigPicture"] = "Big Picture (TV and controller)", ["bpAutoMain"] = "Open Big Picture when a controller connects",
        ["bp"] = "Big Picture", ["bpHome"] = "Home", ["bpLibrary"] = "Library", ["bpSearch"] = "Search", ["bpSettings"] = "Settings",
        ["bpCont"] = "Continue playing", ["bpFavs"] = "Favorites", ["bpInstalled"] = "Installed", ["bpRecent"] = "Recently Added",
        ["bpPlay"] = "Play", ["bpDetails"] = "Details", ["bpAddFav"] = "Add to favorites", ["bpInFav"] = "In favorites",
        ["bpUpdAvail"] = "Update available",
        ["bpNever"] = "Not played yet", ["bpToday"] = "Today", ["bpYesterday"] = "Yesterday",
        ["bpLongAgo"] = "Over a year ago",
        ["bpLastLong"] = "Last played {0}",
        ["bpNtTitle"] = "Notifications", ["bpNtClear"] = "Clear all", ["bpNtDismiss"] = "Dismiss",
        ["bpNtEmpty"] = "You’re all caught up", ["bpNtEmptySub"] = "New notifications show up here.", ["bpNtShow"] = "Show",
        ["bpSeeAll"] = "See all", ["bpAll"] = "All", ["bpSelect"] = "Select", ["bpOpen"] = "Open", ["bpBack"] = "Back", ["bpClose"] = "Close",
        ["bpGuide"] = "Menu", ["bpFilter"] = "Platform", ["bpType"] = "Type", ["bpDel"] = "Delete", ["bpSpace"] = "Space", ["bpDone"] = "Done",
        ["bpResults"] = "Results", ["bpTryOther"] = "Try a different word", ["bpNoResFor"] = "No results for “{0}”", ["bpSearchPh"] = "Search your library…",
        ["bpUpdTitle"] = "{0} has an update",
        ["bpUpdText"] = "Steam downloads it before the game starts. Update now in Steam, or try to play the current version if Steam allows it.",
        ["bpUpdNow"] = "Update in Steam", ["bpPlayAnyway"] = "Play anyway", ["bpCancel"] = "Cancel",
        ["bpStarting"] = "Starting {0}", ["bpLaunched"] = "{0} is running", ["bpViaPlat"] = "Through {0}. Play time starts counting once the game opens.",
        ["bpWaitPlat"] = "Waiting for {0} to open the game…", ["bpFailTitle"] = "Couldn't start {0}",
        ["bpOpenLauncher"] = "Open the launcher", ["bpOpenLauncherOf"] = "Open {0}", ["bpLauncherOpening"] = "Opening {0}", ["bpRetry"] = "Try again",
        ["bpOpenedSteam"] = "Opened {0} in Steam", ["bpNewsOpened"] = "The news opens in Steam",
        ["bpResume"] = "Resume", ["bpNotifications"] = "Notifications", ["bpExitBp"] = "Exit Big Picture", ["bpCloseApp"] = "Close Halawa",
        ["bpExiting"] = "Back to the desktop window", ["bpGameSettings"] = "Game settings",
        ["bpPlayTime"] = "Play time", ["bpLastPlayed"] = "Last played", ["bpSize"] = "Size on disk", ["bpPlatform"] = "Platform", ["bpNews"] = "News",
        ["bpNoNews"] = "No news for this game right now.",
        ["bpSecGeneral"] = "General", ["bpSecController"] = "Controller", ["bpSecDisplay"] = "Display", ["bpSecLanguage"] = "Language", ["bpSecExit"] = "Exit",
        ["bpAutoBp"] = "Open Big Picture when a controller connects", ["bpAutoBpN"] = "Works with Xbox, PlayStation and most controllers Windows and Linux recognise.",
        ["bpStartBp"] = "Start Halawa in Big Picture", ["bpStartBpN"] = "Opens full screen instead of the normal window.",
        ["bpExitDisc"] = "Leave Big Picture when the controller disconnects", ["bpExitDiscN"] = "Returns to the normal window after a few seconds.",
        ["bpGlyphs"] = "Button icons", ["bpGAuto"] = "Auto", ["bpGlyphsN"] = "Auto matches the connected controller.",
        ["bpSwapAB"] = "Swap confirm and back", ["bpSwapABN"] = "For Nintendo-style controllers.",
        ["bpTextSize"] = "Text size", ["bpTNormal"] = "Normal", ["bpTLarge"] = "Large", ["bpTextSizeN"] = "Large is easier to read from the sofa.",
        ["bpShowClock"] = "Show the clock", ["bpShowClockN"] = "In the top bar and the menu.",
        ["bpMotionInfo"] = "Animations follow Reduce motion in Settings › Appearance.",
        ["bpLangNote"] = "The interface direction follows the language.",
        ["bpExitRow"] = "Exit Big Picture", ["bpExitRowN"] = "Big Picture closes and the normal Halawa window opens. The controller still works in the library.",
        ["bpNtUpdT"] = "{0}: update available", ["bpNtUpdB"] = "Steam downloads it before the game starts.", ["bpNtScanT"] = "Scan finished",
        ["bpNtAppT"] = "A new version of Halawa Hub", ["bpNtAppB"] = "Open the normal window, then Settings › Updates, to install it.", ["bpNtFreeT"] = "Free this week on Epic",
        ["bpNow"] = "Now",
        ["bpWhyDrive"] = "The drive isn't connected", ["bpWhyFiles"] = "The game files are missing", ["bpWhyLauncher"] = "The launcher isn't installed", ["bpWhySteam"] = "Steam isn't signed in",
        ["bpController"] = "Controller connected", ["bpNoController"] = "Keyboard", ["bpSaved"] = "Changes are saved automatically",
        ["bpHideGame"] = "Hide from library", ["bpHideGameN"] = "Bring it back from Settings › Library › Show hidden games in the normal window.",
        ["bpMoreSettings"] = "More settings in the normal window", ["bpMoreSettingsN"] = "Name, cover and launch options. Big Picture comes back when you close it.",
        ["bpHiddenToast"] = "{0} is hidden from the library",
    };
}
