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
}
