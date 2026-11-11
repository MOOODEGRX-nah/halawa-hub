namespace HalawaHub.App.Services;

/// إعدادات البرنامج — تعدّل الآن من داخل صفحة Settings مباشرة (ملف
/// config.json يبقى موجود كخيار احتياطي للتعديل اليدوي لو حبيت)
public class AppConfig
{
    /// <summary>
    /// مفتاح API مجاني من SteamGridDB لجلب أغلفة GOG/Epic/Riot/Xbox
    /// (Steam عنده مصدر مباشر بدون مفتاح، ما يحتاجه).
    /// احصل عليه من: https://www.steamgriddb.com/profile/preferences (تبويب API)
    /// اتركه فاضي لو ما تبي هذي الميزة.
    /// في الذاكرة فقط: يُحفظ مشفّرًا عبر SecretStore في SteamGridDbApiKeyRef.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SteamGridDbApiKey { get; set; } = "";

    /// الصيغة القديمة (نص صريح). تُقرأ من الملفات القديمة ثم تُرحَّل وتُحذف من الملف.
    [System.Text.Json.Serialization.JsonPropertyName("SteamGridDbApiKey")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyPlainSteamGridDbApiKey { get; set; }

    /// مرجع المفتاح المحفوظ: dpapi:… أو secret-service:… أو file:…
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SteamGridDbApiKeyRef { get; set; }

    /// آخر قيمة حُفظت، حتى لا نعيد التشفير (أو نستدعي secret-tool) مع كل حفظ للإعدادات
    [System.Text.Json.Serialization.JsonIgnore]
    internal string? SavedSteamGridDbApiKey { get; set; }

    /// آخر إصدار شافه المستخدم فعليًا — نستخدمه لمعرفة هل نعرض نافذة
    /// "ما الجديد" أو لا (نعرضها بس أول مرة يفتح فيها إصدار جديد)
    public string LastSeenVersion { get; set; } = "";

    /// لغة الواجهة: "ar" أو "en". الملفات القديمة بدون هذا الحقل تفتح بالعربية
    public string Language { get; set; } = "ar";

    /// إظهار الألعاب التي أخفاها المستخدم من المكتبة
    public bool ShowHiddenGames { get; set; }

    /// آخر عمليات البحث، تظهر كاقتراحات عند فتح البحث
    public List<string> RecentSearches { get; set; } = new();

    /// ترتيب المكتبة: name, lastplayed, platform, playtime, added
    public string SortMode { get; set; } = "name";

    /// عكس اتجاه الترتيب
    public bool SortReverse { get; set; }

    /// وقت اللعب وآخر تشغيل على البطاقات
    public bool ShowPlayTimeOnCards { get; set; } = true;

    // ===== الإعدادات العامة (المرحلة 5) =====

    /// زر الإغلاق: "tray" يصغّر للصينية، "exit" يغلق البرنامج
    public string CloseAction { get; set; } = "tray";

    /// البدء مصغّرًا (للصينية إن وُجدت)
    public bool StartMinimized { get; set; }

    /// تبويب المكتبة عند الفتح: 0 الرئيسي، 1 المفضل، 2 المثبتة، 3 المضاف حديثًا
    public int StartTab { get; set; }

    /// تصغير Halawa عند بدء لعبة
    public bool MinimizeOnGameStart { get; set; }

    /// إغلاق Steam بعد اللعبة إذا كان Halawa هو من شغّله
    public bool CloseSteamAfterGame { get; set; }

    /// تقليل الحركة: "off" (الحركة تعمل)، "on"، "auto" (يتبع النظام)
    public string ReduceMotion { get; set; } = "off";

    /// حركة الغلاف عند المرور بالفأرة
    public bool CoverHoverAnimation { get; set; } = true;

    /// عرض المكتبة: "grid" أو "list"
    public string LibraryLayout { get; set; } = "grid";

    /// حجم البطاقة: "small" أو "comfortable" أو "large"
    public string CardSize { get; set; } = "comfortable";

    /// الثيم: dark, oled, light — ولون التمييز: red, blue, purple, green, orange
    public string Theme { get; set; } = "dark";
    public string Accent { get; set; } = "red";

    /// استيراد وقت اللعب من Steam (يُعرض الأكبر بين Halawa وSteam)
    public bool ImportSteamPlaytime { get; set; } = true;

    /// SteamID64 للحساب المختار؛ فارغ = آخر حساب سجّل الدخول
    public string SteamAccountId { get; set; } = "";

    // ===== المرحلة 6 =====

    /// فئات الإشعارات داخل البرنامج
    public bool NotifyGameUpdates { get; set; } = true;
    public bool NotifyLaunchFailed { get; set; } = true;
    public bool NotifyScan { get; set; } = true;
    public bool NotifyAppUpdate { get; set; } = true;

    /// قسم "مجانية هذا الأسبوع" من Epic في الصفحة الرئيسية
    public bool ShowFreeGames { get; set; } = true;

    /// معالج أول تشغيل: يُكتب true فقط عند إنشاء الملف لأول مرة، فالملفات القديمة (بلا الحقل) لا تراه
    public bool ShowOnboarding { get; set; }
}
