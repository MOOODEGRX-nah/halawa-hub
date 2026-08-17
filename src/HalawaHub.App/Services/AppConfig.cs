namespace HalawaHub.App.Services;

/// إعدادات اختيارية للبرنامج، يعدّلها المستخدم يدويًا بملف config.json
public class AppConfig
{
    /// <summary>
    /// مفتاح API مجاني من SteamGridDB لجلب أغلفة GOG/Epic/Riot/Xbox
    /// (Steam عنده مصدر مباشر بدون مفتاح، ما يحتاجه).
    /// احصل عليه من: https://www.steamgriddb.com/profile/preferences (تبويب API)
    /// اتركه فاضي لو ما تبي هذي الميزة.
    /// </summary>
    public string SteamGridDbApiKey { get; set; } = "";
}
