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
    /// </summary>
    public string SteamGridDbApiKey { get; set; } = "";

    /// آخر إصدار شافه المستخدم فعليًا — نستخدمه لمعرفة هل نعرض نافذة
    /// "ما الجديد" أو لا (نعرضها بس أول مرة يفتح فيها إصدار جديد)
    public string LastSeenVersion { get; set; } = "";

    /// لغة الواجهة: "ar" أو "en". الملفات القديمة بدون هذا الحقل تفتح بالعربية
    public string Language { get; set; } = "ar";

    /// إظهار الألعاب التي أخفاها المستخدم من المكتبة
    public bool ShowHiddenGames { get; set; }

    /// آخر عمليات البحث، تظهر كاقتراحات عند فتح البحث
    public List<string> RecentSearches { get; set; } = new();
}
