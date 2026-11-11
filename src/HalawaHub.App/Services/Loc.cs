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

    /// للاختبارات: قراءة نص بلغة محددة بدون تغيير اللغة الحالية، فهي حالة عامة
    /// تتشاركها كل النوافذ، وتغييرها من اختبار يعمل بالتوازي يكسر اختبارات الواجهة
    internal static string? Lookup(string language, string key) =>
        (language == English ? En : Ar).TryGetValue(key, out var value) ? value : null;

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
        ["sgdb"] = "SteamGridDB (أغلفة GOG/Epic/Riot/Xbox/Heroic/Lutris)", ["keyPh"] = "الصق المفتاح هنا...",
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
        ["launchDriveMissing"] = "القرص الذي عليه {0} غير موصول ({1}). صِل القرص ثم حاول مجددًا.",
        ["launchFilesMissing"] = "ملفات {0} غير موجودة: {1}",
        ["launchLauncherMissing"] = "لم يُعثر على {1} لتشغيل {0}. ثبّته أو افتحه ثم حاول مجددًا.",
        ["launchSteamSignedOut"] = "Steam غير مسجَّل الدخول. سجّل الدخول في Steam لتبدأ {0}.",
        ["launchTimeout"] = "لم تبدأ {0} خلال 3 دقائق، فلم يُحسب وقت لعب.",
        ["launchAlreadyRunning"] = "{0} تعمل مسبقًا، فلن يُحسب وقت هذه الجلسة.",
        ["uninstallFailed"] = "تعذر فتح نافذة إلغاء التثبيت: {0}",
        ["uninstallUnsupported"] = "إلغاء التثبيت غير مدعوم بعد لهذه المنصة. احذفها من مشغّل المنصة نفسه.",
        ["openLocFailed"] = "تعذر فتح مكان الملفات: {0}", ["launchPh"] = "مثال: -novid -dx11",
        ["updAvailable"] = "يتوفر إصدار جديد: v{0} (لديك v{1})", ["updUpToDate"] = "البرنامج محدّث لآخر إصدار.",
        ["updCheckFailed"] = "تعذّر التحقق من التحديثات. تأكد من اتصالك بالإنترنت وحاول مرة أخرى.",
        ["install"] = "تثبيت الآن", ["openRelease"] = "فتح صفحة الإصدار",
        ["updDownloading"] = "جاري تحميل التحديث...", ["updExtracting"] = "جاري فك الملفات...",
        ["updRestarting"] = "جاري إعادة التشغيل...", ["updReady"] = "التحديث جاهز، البرنامج يعيد التشغيل الآن...",
        ["updChecksum"] = "فشل التحقق من سلامة الملف، أُلغي التثبيت.",
        ["updChecksumMissing"] = "لا يوجد رمز تحقق لهذا التحديث، فلم يُثبَّت. نزّله من صفحة الإصدار.",
        ["secretDpapi"] = "محفوظ مشفّرًا لحسابك في ويندوز (DPAPI).",
        ["secretKeyring"] = "محفوظ في مخزن مفاتيح النظام (Secret Service).",
        ["secretFile"] = "محفوظ في ملف لا يقرؤه غيرك (0600)، لعدم وجود مخزن مفاتيح في النظام.",
["sPlugins"] = "الإضافات",        ["pluginsNote"] = "ملفات DLL في مجلد Plugins تضيف منصات أو أدوات. الإضافة الجديدة لا تعمل حتى توافق عليها، وأي تغيير في ملفها يعيد السؤال.",        ["pluginsEmpty"] = "لا توجد إضافات في مجلد Plugins.",        ["pluginOn"] = "تعمل", ["pluginOff"] = "متوقفة",        ["pluginWaiting"] = "جديدة، بانتظار موافقتك", ["pluginChangedWaiting"] = "تغيّر ملفها، بانتظار موافقتك",        ["pluginFailed"] = "تعذر تحميلها: {0}", ["pluginBundled"] = "تأتي مع البرنامج",        ["pluginAskTitle"] = "إضافة جديدة", ["pluginAskChangedTitle"] = "تغيّر ملف إضافة",        ["pluginAskBody"] = "وُجدت الإضافة {0} في مجلد Plugins. الإضافة برنامج يعمل بصلاحياتك، فلا تسمح إلا بما تثق بمصدره.",        ["pluginAllow"] = "اسمح بتشغيلها", ["pluginDeny"] = "لا تشغّلها",        ["pluginLoadFailed"] = "تعذر تشغيل الإضافة {0}. التفاصيل في سجل البرنامج.",
["stUpdate"] = "تحديث متوفر", ["stUpdating"] = "يتحدث الآن", ["stRepair"] = "تحتاج إصلاحًا",        ["stRunning"] = "تعمل", ["stDrive"] = "القرص غير موصول", ["stOk"] = "جاهزة", ["stNone"] = "",        ["openInLauncher"] = "افتح في {0}", ["updateInLauncher"] = "حدّث في {0}", ["verifyFiles"] = "افحص الملفات",        ["uninstallInLauncher"] = "افتح المشغّل لإلغاء التثبيت",        ["updNoteSteam"] = "الحالة من ملفات Steam نفسها. لا يوفّر Steam أمرًا لبدء التحديث من برنامج آخر، فزر التحديث يفتح اللعبة في Steam حيث زر التحديث.",        ["updNoteEpic"] = "يكشف Halawa حاجة الإصلاح من ملفات Epic. أما التحديثات فلا يعرفها Epic إلا بعد تسجيل الدخول فيه، فافتحه للتحقق.",        ["updNoteOther"] = "{0} لا يشارك حالة التحديث مع البرامج الأخرى، فافتحه للتحقق من التحديثات.",        ["updNoteNone"] = "{0} لا يشارك حالة التحديث مع البرامج الأخرى.",
        ["updNoteManual"] = "لا يتتبع Halawa تحديثات الألعاب التي تضيفها بنفسك.",        ["openLauncherFailed"] = "تعذر فتح المشغّل: {0}", ["verifyStarted"] = "طُلب من Steam فحص ملفات {0}.",        ["updatePendingTitle"] = "{0} لها تحديث", ["playAnyway"] = "شغّلها على أي حال", ["cancel"] = "إلغاء",        ["updatePendingSteam"] = "قد ينزّل Steam التحديث قبل أن تبدأ اللعبة حتى لو اخترت التشغيل.",        ["updatePendingOther"] = "يمكنك تحديثها من مشغّلها أولًا، أو تشغيلها كما هي.",
["sortPlayTime"] = "وقت اللعب", ["sortAdded"] = "تاريخ الإضافة", ["sortReverse"] = "اعكس الترتيب",        ["showStats"] = "وقت اللعب على البطاقات", ["showStatsNote"] = "يظهر وقت اللعب وآخر تشغيل تحت اسم اللعبة.",        ["lastPlayedLbl"] = "آخر تشغيل", ["scanThisPc"] = "افحص هذا الجهاز", ["tryAgain"] = "حاول مجددًا",        ["quickSearchPh"] = "ابحث عن لعبة…", ["quickSearchHint"] = "↑ ↓ للتنقل · Enter للفتح · Esc للإغلاق · Ctrl+K من أي مكان",        ["addGame"] = "إضافة لعبة", ["gameFiles"] = "ملفات الألعاب", ["platManual"] = "يدوية",        ["addGameFailed"] = "تعذرت إضافة {0}: الملف غير مدعوم أو تالف.",        ["manualTarget"] = "الملف الذي يُشغَّل", ["changeFile"] = "غيّر الملف", ["removeManual"] = "أزلها من Halawa",        ["removeManualNote"] = "الإزالة تخص قائمة Halawa فقط، ولا يُحذف أي ملف من جهازك.",        ["desktopShortcut"] = "اختصار على سطح المكتب", ["copyId"] = "انسخ معرّف اللعبة",        ["shortcutCreated"] = "أُنشئ اختصار {0} على سطح المكتب.", ["shortcutFailed"] = "تعذر إنشاء الاختصار: {0}",        ["idCopied"] = "نُسخ المعرّف: {0}",        ["news"] = "الأخبار", ["newsLoading"] = "يجلب الأخبار…", ["newsNone"] = "لا توجد أخبار، أو لا يوجد اتصال بالإنترنت.",
        ["sGeneral"] = "عام", ["sLibrary"] = "المكتبة", ["sPlatforms"] = "المنصات", ["sTools"] = "الأدوات",
        ["startMin"] = "ابدأ مصغّرًا", ["startMinNote"] = "يفتح في الصينية (أو مصغّرًا في شريط المهام إن لم تتوفر).",
        ["closeBtn"] = "زر الإغلاق", ["closeTray"] = "صغّر للصينية", ["closeExit"] = "أغلق البرنامج",
        ["closeBtnNote"] = "ما يحدث عند الضغط على ✕. الإغلاق من قائمة الصينية يغلق البرنامج دائمًا.",
        ["startTab"] = "الصفحة عند الفتح", ["startTabNote"] = "تبويب المكتبة الذي يظهر أولًا.",
        ["minOnPlay"] = "صغّر Halawa عند بدء لعبة", ["minOnPlayNote"] = "يخفي النافذة عن طريق اللعبة.",
        ["closeSteam"] = "أغلق Steam بعد اللعبة", ["closeSteamNote"] = "فقط إذا كان Halawa هو من شغّل Steam لهذه اللعبة.",
        ["layout"] = "عرض المكتبة", ["layoutGrid"] = "شبكة", ["layoutList"] = "قائمة",
        ["layoutNote"] = "القائمة تعرض الاسم والمنصة ووقت اللعب والحجم على القرص.",
        ["cardSize"] = "حجم البطاقة", ["sizeSmall"] = "صغير", ["sizeComfortable"] = "مريح", ["sizeLarge"] = "كبير",
        ["cardSizeNote"] = "عدد البطاقات في كل صف يتبع الحجم وعرض النافذة.",
        ["defaultSort"] = "الترتيب الافتراضي", ["defaultSortNote"] = "يُحفظ ويُستخدم في كل مرة تفتح فيها البرنامج.",
        ["theme"] = "الثيم", ["themeDark"] = "داكن", ["themeOled"] = "أسود OLED", ["themeLight"] = "فاتح",
        ["themeNote"] = "يتغير فورًا. الأسود يوفّر الطاقة على شاشات OLED.",
        ["accent"] = "لون التمييز", ["accentNote"] = "لون الأزرار والعناوين والتوهج.",
        ["accentRed"] = "أحمر", ["accentBlue"] = "أزرق", ["accentPurple"] = "بنفسجي", ["accentGreen"] = "أخضر", ["accentOrange"] = "برتقالي",
        ["reduceMotion"] = "تقليل الحركة", ["motionOff"] = "لا", ["motionOn"] = "نعم", ["motionAuto"] = "حسب النظام",
        ["reduceMotionNote"] = "يوقف حركات الظهور والانزلاق. «حسب النظام» يتبع إعداد تأثيرات الحركة في ويندوز أو KDE أو GNOME.",
        ["motionAutoReduced"] = "النظام يطلب تقليل الحركة الآن.", ["motionAutoFull"] = "النظام لا يطلب تقليل الحركة الآن.",
        ["coverHover"] = "حركة الغلاف عند المرور", ["coverHoverNote"] = "ترتفع البطاقة قليلًا عند مرور الفأرة عليها.",
        ["platformsNote"] = "ما يراه Halawa من كل منصة. ✓ متوفر، ◐ جزئي، ✗ غير متاح (المنصة لا تشاركه).",
        ["redetect"] = "أعد الكشف", ["platDetected"] = "موجودة", ["platNotFound"] = "غير موجودة", ["platGames"] = "{0} لعبة في المكتبة",
        ["capGames"] = "الألعاب المثبتة", ["capPlayTime"] = "وقت اللعب", ["capUpdates"] = "التحديثات", ["capRepairOnly"] = "الإصلاح فقط",
        ["capRunning"] = "تعمل من خارج Halawa",
        ["openLogs"] = "افتح مجلد السجلات", ["openData"] = "افتح مجلد البيانات", ["clearCovers"] = "امسح ذاكرة الأغلفة",
        ["toolsNote"] = "مسح ذاكرة الأغلفة لا يحذف أغلفتك المخصصة؛ تُنزَّل الأغلفة من جديد عند الحاجة.",
        ["coverCacheCleared"] = "حُذف {0} ملف من ذاكرة الأغلفة ({1}).",
        ["dangerZone"] = "منطقة الخطر", ["deleteAll"] = "احذف كل بيانات Halawa",
        ["deleteAllNote"] = "يحذف الإعدادات والمفضلة ووقت اللعب والسجل والأغلفة المخصصة. لا يمس ألعابك ولا المشغّلات.",
        ["deleteAllTitle"] = "حذف كل بيانات Halawa؟",
        ["deleteAllBody"] = "سيُغلق البرنامج الآن، وتُحذف البيانات عند فتحه في المرة القادمة. لا يمكن التراجع.",
        ["deleteAllDeletes"] = "يُحذف: الإعدادات، المفضلة، وقت اللعب وآخر تشغيل، الأسماء والأغلفة المخصصة، الألعاب المضافة يدويًا، موافقات الإضافات، ذاكرة الأغلفة، السجلات، مفتاح SteamGridDB.",
        ["deleteAllKeeps"] = "يبقى: ألعابك وملفاتها، Steam وEpic وغيرها، وبرنامج Halawa نفسه.",
        ["deleteAllConfirm"] = "احذف وأغلق البرنامج", ["deleteAllFailed"] = "تعذر طلب الحذف: {0}",
        ["steamImport"] = "استورد وقت اللعب من Steam", ["steamImportNote"] = "يقرأ الوقت الذي سجّله Steam على هذا الجهاز، ويعرض الأكبر بين وقت Halawa ووقت Steam.",
        ["steamAccount"] = "حساب Steam", ["steamAccountNote"] = "الحساب الذي يُقرأ وقت لعبه. الافتراضي آخر حساب سجّل الدخول.",
        ["sCollections"] = "المجموعات", ["sSubscriptions"] = "الاشتراكات",
        ["colNote"] = "المجموعات تظهر كشريط في المكتبة. اليدوية تختار ألعابها من نافذة كل لعبة، والذكية تدخلها الألعاب وتخرج منها حسب شروطك.",
        ["colNewManual"] = "مجموعة يدوية جديدة", ["colNewSmart"] = "مجموعة ذكية جديدة", ["colEdit"] = "تعديل المجموعة",
        ["colEmpty"] = "لا توجد مجموعات بعد.", ["colManual"] = "يدوية", ["colSmart"] = "ذكية",
        ["colEditBtn"] = "تعديل", ["colHide"] = "إخفاء", ["colShow"] = "إظهار", ["colDelete"] = "حذف", ["colDrag"] = "اسحب لإعادة الترتيب",
        ["colBuiltIn"] = "صفحات المفضل والمثبتة والمضاف حديثًا ثابتة في Halawa ولا تُعدَّل، فلا تحتاج استعادة.",
        ["colName"] = "اسم المجموعة", ["colIcon"] = "الأيقونة", ["colMatch"] = "تدخل اللعبة إذا تحقق",
        ["colAll"] = "كل الشروط", ["colAny"] = "أي شرط", ["colAddRule"] = "أضف شرطًا", ["colRemoveRule"] = "احذف الشرط", ["colSave"] = "حفظ",
        ["colManualNote"] = "بعد الحفظ، أضف الألعاب من نافذة إعدادات كل لعبة (قسم عام).",
        ["colNeedName"] = "اكتب اسمًا للمجموعة.", ["colNeedRule"] = "المجموعة الذكية تحتاج شرطًا واحدًا على الأقل.",
        ["colBadRule"] = "أكمل كل الشروط: اختر المنصة أو اكتب رقمًا أو نصًا.",
        ["colDeleteTitle"] = "حذف المجموعة «{0}»؟", ["colDeleteBody"] = "تُحذف المجموعة فقط. الألعاب نفسها لا تتأثر.",
        ["cf_platform"] = "المنصة", ["cf_playtime"] = "وقت اللعب", ["cf_lastplayed"] = "آخر تشغيل", ["cf_added"] = "أُضيفت",
        ["cf_installed"] = "مثبتة", ["cf_favorite"] = "مفضلة", ["cf_name"] = "الاسم",
        ["co_is"] = "هي", ["co_isnot"] = "ليست", ["co_atleast"] = "على الأقل (ساعات)", ["co_atmost"] = "على الأكثر (ساعات)",
        ["co_within"] = "خلال (أيام)", ["co_notwithin"] = "ليس خلال (أيام)", ["co_never"] = "لم تُلعب أبدًا",
        ["co_yes"] = "نعم", ["co_no"] = "لا", ["co_contains"] = "يحتوي",
        ["cvHours"] = "ساعات", ["cvDays"] = "أيام", ["cvText"] = "نص",
        ["notifications"] = "الإشعارات", ["ntEmpty"] = "لا توجد إشعارات.", ["ntClear"] = "امسح الكل", ["ntDismiss"] = "إخفاء",
        ["ntScan"] = "انتهى الفحص: {0} لعبة، منها {1} جديدة.", ["ntGameUpdate"] = "{0}: تحديث متوفر.",
        ["ntAppUpdate"] = "يتوفر إصدار جديد من Halawa-Hub. افتح الإعدادات ثم التحديثات.",
        ["ntfUpdate"] = "تحديثات الألعاب", ["ntfUpdateNote"] = "إشعار عند ظهور تحديث للعبة.",
        ["ntfLaunch"] = "فشل التشغيل", ["ntfLaunchNote"] = "إشعار عند تعذر تشغيل لعبة مع السبب.",
        ["ntfScan"] = "انتهاء الفحص", ["ntfScanNote"] = "إشعار بعدد الألعاب الجديدة بعد كل فحص.",
        ["ntfApp"] = "تحديث Halawa", ["ntfAppNote"] = "إشعار عند توفر إصدار جديد من البرنامج.",
        ["wizTitle"] = "مرحبًا بك في Halawa-Hub", ["wizNext"] = "التالي", ["wizBack"] = "رجوع", ["wizFinish"] = "ابدأ", ["wizSkip"] = "تخطَّ",
        ["wizLang"] = "اختر لغة الواجهة.", ["wizLangNote"] = "يمكنك تغييرها لاحقًا من الإعدادات.",
        ["wizPlatforms"] = "هذه المنصات التي وجدها Halawa على جهازك. ألعابها تظهر في المكتبة تلقائيًا.",
        ["wizKey"] = "مفتاح SteamGridDB (اختياري): يجلب أغلفة ألعاب المنصات غير Steam.",
        ["wizKeyNote"] = "اتركه فارغًا إن لم ترده. يُحفظ مشفّرًا على جهازك، ويمكن إضافته لاحقًا من الإعدادات.",
        ["subNote"] = "تتبّع محلي لاشتراكات الألعاب (مثل Game Pass). لا يتصل Halawa بأي خدمة، والأرقام بعملتك أنت.",
        ["subTotal"] = "المجموع الشهري: {0}", ["subEmpty"] = "لا توجد اشتراكات.", ["subAdd"] = "أضف اشتراكًا",
        ["subNamePh"] = "الاسم", ["subCostPh"] = "التكلفة", ["subDate"] = "تاريخ التجديد", ["subMonthly"] = "شهري", ["subYearly"] = "سنوي",
        ["subAddBtn"] = "أضف", ["subRemove"] = "احذف الاشتراك", ["subMonth"] = "شهر", ["subYear"] = "سنة",
        ["subRenews"] = "يتجدد {0} (بعد {1} يوم)", ["subToday"] = "يتجدد اليوم",
        ["subDateNote"] = "اكتب أي تاريخ تجديد تعرفه بصيغة سنة-شهر-يوم (مثل 2026-12-01)، وHalawa يحسب التجديد القادم.",
        ["freeThisWeek"] = "مجانية هذا الأسبوع على Epic", ["freeUntil"] = "مجانية حتى {0}",
        ["showFree"] = "مجانية هذا الأسبوع", ["showFreeNote"] = "تعرض الصفحة الرئيسية ألعاب Epic المجانية حاليًا. تحتاج إنترنت، وتختفي إن تعذر جلبها.",
        ["details"] = "التفاصيل", ["storeLoading"] = "يجلب بيانات اللعبة…",
        ["storeNone"] = "لا تتوفر بيانات لهذه اللعبة الآن. تحتاج اتصالًا بالإنترنت في أول مرة.",
        ["storeDeveloper"] = "المطوّر", ["storePublisher"] = "الناشر", ["storeRelease"] = "تاريخ الإصدار",
        ["storeMedia"] = "لقطات الشاشة", ["storeReqWindows"] = "متطلبات ويندوز", ["storeReqLinux"] = "متطلبات لينكس",
        ["storeSource"] = "البيانات من متجر Steam، وتُحفظ على جهازك أسبوعًا.",
        ["deckVerified"] = "Steam Deck: موثَّقة", ["deckPlayable"] = "Steam Deck: قابلة للعب", ["deckUnsupported"] = "Steam Deck: غير مدعومة", ["deckUnknown"] = "Steam Deck: غير معروف",
        ["mediaPrev"] = "السابقة", ["mediaNext"] = "التالية",
        ["installGame"] = "تثبيت", ["installAsked"] = "طُلب من Steam تثبيت {0}.",
        ["steamWebKey"] = "مفتاح Steam Web API (اختياري)",
        ["steamWebKeyNote"] = "يعرض ألعاب Steam التي تملكها ولم تثبّتها، مع زر تثبيت. المفتاح مجاني من Steam للحسابات غير المحدودة، ويُحفظ مشفّرًا على جهازك. اتركه فارغًا لإيقاف الميزة.",
        ["ownedCount"] = "وُجدت {0} لعبة مملوكة على Steam.", ["ownedFailed"] = "تعذر جلب ألعابك من Steam: تحقق من المفتاح، ومن أن تفاصيل الألعاب في ملفك الشخصي عامة، ومن الإنترنت.",
        ["ownedNoAccount"] = "لم يُعثر على حساب Steam على هذا الجهاز.",
        ["mergeDup"] = "ادمج اللعبة المكررة", ["mergeDupNote"] = "اللعبة الموجودة على أكثر من منصة تظهر ببطاقة واحدة، وتسألك عند التشغيل من أي منصة.",
        ["launchFromTitle"] = "من أين تشغّل {0}؟", ["launchFromBody"] = "هذه اللعبة موجودة على أكثر من منصة.",
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
        ["sgdb"] = "SteamGridDB (GOG/Epic/Riot/Xbox/Heroic/Lutris covers)", ["keyPh"] = "Paste key here...",
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
        ["launchDriveMissing"] = "The drive for {0} isn't connected ({1}). Connect it and try again.",
        ["launchFilesMissing"] = "{0}'s files weren't found: {1}",
        ["launchLauncherMissing"] = "{1} wasn't found, so {0} can't start. Install or open it and try again.",
        ["launchSteamSignedOut"] = "Steam isn't signed in. Sign in to Steam to start {0}.",
        ["launchTimeout"] = "{0} didn't start within 3 minutes, so no play time was counted.",
        ["launchAlreadyRunning"] = "{0} is already running, so this session's play time isn't counted.",
        ["uninstallFailed"] = "Couldn't open the uninstall window: {0}",
        ["uninstallUnsupported"] = "Uninstall isn't supported for this platform yet. Remove it from the platform's own launcher.",
        ["openLocFailed"] = "Couldn't open the file location: {0}", ["launchPh"] = "Example: -novid -dx11",
        ["updAvailable"] = "New version available: v{0} (you have v{1})", ["updUpToDate"] = "You are on the latest version.",
        ["updCheckFailed"] = "Couldn't check for updates. Check your internet connection and try again.",
        ["install"] = "Install now", ["openRelease"] = "Open release page",
        ["updDownloading"] = "Downloading update...", ["updExtracting"] = "Extracting files...",
        ["updRestarting"] = "Restarting...", ["updReady"] = "Update ready, restarting now...",
        ["updChecksum"] = "File verification failed, install cancelled.",
        ["updChecksumMissing"] = "This update has no checksum to verify it, so it wasn't installed. Download it from the release page.",
        ["secretDpapi"] = "Stored encrypted for your Windows account (DPAPI).",
        ["secretKeyring"] = "Stored in your system keyring (Secret Service).",
        ["secretFile"] = "Stored in a file only your user can read (0600). No system keyring was found.",
["sPlugins"] = "Plugins",        ["pluginsNote"] = "DLL files in the Plugins folder add platforms or tools. A new plugin doesn't run until you approve it, and any change to its file asks again.",        ["pluginsEmpty"] = "There are no plugins in the Plugins folder.",        ["pluginOn"] = "On", ["pluginOff"] = "Off",        ["pluginWaiting"] = "New, waiting for your approval", ["pluginChangedWaiting"] = "File changed, waiting for your approval",        ["pluginFailed"] = "Couldn't load: {0}", ["pluginBundled"] = "Comes with Halawa",        ["pluginAskTitle"] = "New plugin", ["pluginAskChangedTitle"] = "A plugin file changed",        ["pluginAskBody"] = "The plugin {0} was found in the Plugins folder. A plugin is a program that runs with your permissions, so only allow ones from a source you trust.",        ["pluginAllow"] = "Allow it", ["pluginDeny"] = "Don't run it",        ["pluginLoadFailed"] = "Couldn't start the plugin {0}. Details are in the app log.",
["stUpdate"] = "Update available", ["stUpdating"] = "Updating", ["stRepair"] = "Needs repair",        ["stRunning"] = "Running", ["stDrive"] = "Drive not connected", ["stOk"] = "Ready", ["stNone"] = "",        ["openInLauncher"] = "Open in {0}", ["updateInLauncher"] = "Update in {0}", ["verifyFiles"] = "Verify files",        ["uninstallInLauncher"] = "Open the launcher to uninstall",        ["updNoteSteam"] = "Status comes from Steam's own files. Steam has no command to start an update from another app, so the update button opens the game in Steam, where its Update button is.",        ["updNoteEpic"] = "Halawa reads repair needs from Epic's files. Epic only knows about updates after you sign in to it, so open it to check.",        ["updNoteOther"] = "{0} doesn't share update status with other apps, so open it to check for updates.",        ["updNoteNone"] = "{0} doesn't share update status with other apps.",
        ["updNoteManual"] = "Halawa doesn't track updates for games you add yourself.",        ["openLauncherFailed"] = "Couldn't open the launcher: {0}", ["verifyStarted"] = "Asked Steam to verify {0}'s files.",        ["updatePendingTitle"] = "{0} has an update", ["playAnyway"] = "Play anyway", ["cancel"] = "Cancel",        ["updatePendingSteam"] = "Steam may download the update before the game starts, even if you choose to play.",        ["updatePendingOther"] = "You can update it in its launcher first, or play it as it is.",
["sortPlayTime"] = "Play time", ["sortAdded"] = "Date added", ["sortReverse"] = "Reverse order",        ["showStats"] = "Play time on cards", ["showStatsNote"] = "Shows play time and last played under the game's name.",        ["lastPlayedLbl"] = "Last played", ["scanThisPc"] = "Scan this PC", ["tryAgain"] = "Try again",        ["quickSearchPh"] = "Search for a game…", ["quickSearchHint"] = "↑ ↓ to move · Enter to open · Esc to close · Ctrl+K from anywhere",        ["addGame"] = "Add a game", ["gameFiles"] = "Game files", ["platManual"] = "Manual",        ["addGameFailed"] = "Couldn't add {0}: the file isn't supported or is damaged.",        ["manualTarget"] = "File that runs", ["changeFile"] = "Change file", ["removeManual"] = "Remove from Halawa",        ["removeManualNote"] = "This only removes it from Halawa's list. No file is deleted from your PC.",        ["desktopShortcut"] = "Desktop shortcut", ["copyId"] = "Copy game ID",        ["shortcutCreated"] = "Created a desktop shortcut for {0}.", ["shortcutFailed"] = "Couldn't create the shortcut: {0}",        ["idCopied"] = "Copied the ID: {0}",        ["news"] = "News", ["newsLoading"] = "Loading news…", ["newsNone"] = "No news, or no internet connection.",
        ["sGeneral"] = "General", ["sLibrary"] = "Library", ["sPlatforms"] = "Platforms", ["sTools"] = "Tools",
        ["startMin"] = "Start minimized", ["startMinNote"] = "Opens in the tray (or minimized to the taskbar if there's no tray).",
        ["closeBtn"] = "Close button", ["closeTray"] = "Minimize to tray", ["closeExit"] = "Exit Halawa",
        ["closeBtnNote"] = "What happens when you press ✕. Exit in the tray menu always closes Halawa.",
        ["startTab"] = "Page on start", ["startTabNote"] = "The library tab shown first.",
        ["minOnPlay"] = "Minimize Halawa when a game starts", ["minOnPlayNote"] = "Gets the window out of the game's way.",
        ["closeSteam"] = "Close Steam after the game", ["closeSteamNote"] = "Only if Halawa started Steam for this game.",
        ["layout"] = "Library view", ["layoutGrid"] = "Grid", ["layoutList"] = "List",
        ["layoutNote"] = "The list shows name, platform, play time and size on disk.",
        ["cardSize"] = "Card size", ["sizeSmall"] = "Small", ["sizeComfortable"] = "Comfortable", ["sizeLarge"] = "Large",
        ["cardSizeNote"] = "Cards per row follow the size and the window width.",
        ["defaultSort"] = "Default sort", ["defaultSortNote"] = "Remembered and used every time you open Halawa.",
        ["theme"] = "Theme", ["themeDark"] = "Dark", ["themeOled"] = "OLED black", ["themeLight"] = "Light",
        ["themeNote"] = "Changes right away. Black saves power on OLED screens.",
        ["accent"] = "Accent colour", ["accentNote"] = "Colour of buttons, titles and glow.",
        ["accentRed"] = "Red", ["accentBlue"] = "Blue", ["accentPurple"] = "Purple", ["accentGreen"] = "Green", ["accentOrange"] = "Orange",
        ["reduceMotion"] = "Reduce motion", ["motionOff"] = "No", ["motionOn"] = "Yes", ["motionAuto"] = "Follow system",
        ["reduceMotionNote"] = "Stops entrance and sliding animations. Follow system uses the animation setting of Windows, KDE or GNOME.",
        ["motionAutoReduced"] = "Your system asks for reduced motion right now.", ["motionAutoFull"] = "Your system doesn't ask for reduced motion right now.",
        ["coverHover"] = "Cover hover animation", ["coverHoverNote"] = "Cards lift a little when the mouse is over them.",
        ["platformsNote"] = "What Halawa can see from each platform. ✓ available, ◐ partial, ✗ not available (the platform doesn't share it).",
        ["redetect"] = "Re-detect", ["platDetected"] = "Detected", ["platNotFound"] = "Not found", ["platGames"] = "{0} games in the library",
        ["capGames"] = "Installed games", ["capPlayTime"] = "Play time", ["capUpdates"] = "Updates", ["capRepairOnly"] = "Repair only",
        ["capRunning"] = "Running outside Halawa",
        ["openLogs"] = "Open log folder", ["openData"] = "Open data folder", ["clearCovers"] = "Clear cover cache",
        ["toolsNote"] = "Clearing the cover cache keeps your custom covers; covers download again when needed.",
        ["coverCacheCleared"] = "Deleted {0} files from the cover cache ({1}).",
        ["dangerZone"] = "Danger zone", ["deleteAll"] = "Delete all Halawa data",
        ["deleteAllNote"] = "Deletes settings, favorites, play time, history and custom covers. Your games and launchers aren't touched.",
        ["deleteAllTitle"] = "Delete all Halawa data?",
        ["deleteAllBody"] = "Halawa will close now, and the data is deleted the next time it opens. This can't be undone.",
        ["deleteAllDeletes"] = "Deleted: settings, favorites, play time and last played, custom names and covers, games you added, plugin approvals, cover cache, logs, the SteamGridDB key.",
        ["deleteAllKeeps"] = "Kept: your games and their files, Steam, Epic and other launchers, and the Halawa program itself.",
        ["deleteAllConfirm"] = "Delete and close Halawa", ["deleteAllFailed"] = "Couldn't schedule the deletion: {0}",
        ["steamImport"] = "Import play time from Steam", ["steamImportNote"] = "Reads the time Steam recorded on this PC and shows the larger of Halawa's and Steam's.",
        ["steamAccount"] = "Steam account", ["steamAccountNote"] = "The account whose play time is read. Default: the last one that signed in.",
        ["sCollections"] = "Collections", ["sSubscriptions"] = "Subscriptions",
        ["colNote"] = "Collections show as a bar in the library. You pick the games of a manual one from each game's dialog; smart ones fill themselves from your rules.",
        ["colNewManual"] = "New manual collection", ["colNewSmart"] = "New smart collection", ["colEdit"] = "Edit collection",
        ["colEmpty"] = "No collections yet.", ["colManual"] = "Manual", ["colSmart"] = "Smart",
        ["colEditBtn"] = "Edit", ["colHide"] = "Hide", ["colShow"] = "Show", ["colDelete"] = "Delete", ["colDrag"] = "Drag to reorder",
        ["colBuiltIn"] = "The Favorites, Installed and Recently Added pages are fixed in Halawa and can't be edited, so they never need restoring.",
        ["colName"] = "Collection name", ["colIcon"] = "Icon", ["colMatch"] = "A game is included if it matches",
        ["colAll"] = "All rules", ["colAny"] = "Any rule", ["colAddRule"] = "Add a rule", ["colRemoveRule"] = "Remove rule", ["colSave"] = "Save",
        ["colManualNote"] = "After saving, add games from each game's settings dialog (General).",
        ["colNeedName"] = "Give the collection a name.", ["colNeedRule"] = "A smart collection needs at least one rule.",
        ["colBadRule"] = "Finish every rule: pick the platform, or type a number or text.",
        ["colDeleteTitle"] = "Delete the collection \"{0}\"?", ["colDeleteBody"] = "Only the collection is deleted. The games aren't affected.",
        ["cf_platform"] = "Platform", ["cf_playtime"] = "Play time", ["cf_lastplayed"] = "Last played", ["cf_added"] = "Added",
        ["cf_installed"] = "Installed", ["cf_favorite"] = "Favorite", ["cf_name"] = "Name",
        ["co_is"] = "is", ["co_isnot"] = "is not", ["co_atleast"] = "at least (hours)", ["co_atmost"] = "at most (hours)",
        ["co_within"] = "within (days)", ["co_notwithin"] = "not within (days)", ["co_never"] = "never played",
        ["co_yes"] = "yes", ["co_no"] = "no", ["co_contains"] = "contains",
        ["cvHours"] = "hours", ["cvDays"] = "days", ["cvText"] = "text",
        ["notifications"] = "Notifications", ["ntEmpty"] = "No notifications.", ["ntClear"] = "Clear all", ["ntDismiss"] = "Dismiss",
        ["ntScan"] = "Scan finished: {0} games, {1} new.", ["ntGameUpdate"] = "{0}: update available.",
        ["ntAppUpdate"] = "A new version of Halawa-Hub is available. Open Settings, then Updates.",
        ["ntfUpdate"] = "Game updates", ["ntfUpdateNote"] = "Notify when a game gets an update.",
        ["ntfLaunch"] = "Launch failures", ["ntfLaunchNote"] = "Notify when a game can't start, with the reason.",
        ["ntfScan"] = "Scan finished", ["ntfScanNote"] = "Notify with the number of new games after each scan.",
        ["ntfApp"] = "Halawa updates", ["ntfAppNote"] = "Notify when a new version of the app is available.",
        ["wizTitle"] = "Welcome to Halawa-Hub", ["wizNext"] = "Next", ["wizBack"] = "Back", ["wizFinish"] = "Start", ["wizSkip"] = "Skip",
        ["wizLang"] = "Choose the interface language.", ["wizLangNote"] = "You can change it later in Settings.",
        ["wizPlatforms"] = "These are the platforms Halawa found on your PC. Their games show up in the library automatically.",
        ["wizKey"] = "SteamGridDB key (optional): it fetches covers for games that aren't on Steam.",
        ["wizKeyNote"] = "Leave it empty if you don't want it. It's stored encrypted on your PC, and you can add it later in Settings.",
        ["subNote"] = "A local tracker for game subscriptions (like Game Pass). Halawa doesn't connect to any service, and amounts are in your own currency.",
        ["subTotal"] = "Monthly total: {0}", ["subEmpty"] = "No subscriptions.", ["subAdd"] = "Add a subscription",
        ["subNamePh"] = "Name", ["subCostPh"] = "Cost", ["subDate"] = "Renewal date", ["subMonthly"] = "Monthly", ["subYearly"] = "Yearly",
        ["subAddBtn"] = "Add", ["subRemove"] = "Remove subscription", ["subMonth"] = "month", ["subYear"] = "year",
        ["subRenews"] = "Renews {0} (in {1} days)", ["subToday"] = "Renews today",
        ["subDateNote"] = "Type any renewal date you know as year-month-day (like 2026-12-01); Halawa works out the next one.",
        ["freeThisWeek"] = "Free this week on Epic", ["freeUntil"] = "Free until {0}",
        ["showFree"] = "Free this week", ["showFreeNote"] = "Shows Epic's currently free games on the home page. Needs internet, and hides itself if they can't be fetched.",
        ["details"] = "Details", ["storeLoading"] = "Loading game info…",
        ["storeNone"] = "No info is available for this game right now. It needs an internet connection the first time.",
        ["storeDeveloper"] = "Developer", ["storePublisher"] = "Publisher", ["storeRelease"] = "Release date",
        ["storeMedia"] = "Screenshots", ["storeReqWindows"] = "Windows requirements", ["storeReqLinux"] = "Linux requirements",
        ["storeSource"] = "Info comes from the Steam store and is kept on your PC for a week.",
        ["deckVerified"] = "Steam Deck: Verified", ["deckPlayable"] = "Steam Deck: Playable", ["deckUnsupported"] = "Steam Deck: Unsupported", ["deckUnknown"] = "Steam Deck: Unknown",
        ["mediaPrev"] = "Previous", ["mediaNext"] = "Next",
        ["installGame"] = "Install", ["installAsked"] = "Asked Steam to install {0}.",
        ["steamWebKey"] = "Steam Web API key (optional)",
        ["steamWebKeyNote"] = "Shows the Steam games you own but haven't installed, with an Install button. The key is free from Steam for non-limited accounts and is stored encrypted on your PC. Leave it empty to turn this off.",
        ["ownedCount"] = "Found {0} owned games on Steam.", ["ownedFailed"] = "Couldn't get your games from Steam: check the key, that game details are public on your profile, and your internet.",
        ["ownedNoAccount"] = "No Steam account was found on this PC.",
        ["mergeDup"] = "Merge duplicate games", ["mergeDupNote"] = "A game found on several platforms shows as one card and asks which platform to launch from.",
        ["launchFromTitle"] = "Launch {0} from?", ["launchFromBody"] = "This game is on more than one platform.",
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
