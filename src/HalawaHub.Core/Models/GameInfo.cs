namespace HalawaHub.Core.Models;

/// <summary>
/// يمثل لعبة واحدة تم اكتشافها من أي منصة (Steam, GOG, ...).
/// هذا الكائن هو "العملة المشتركة" بين كل الـ Providers والـ Plugins.
/// </summary>
public class GameInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // "Steam", "GOG", "Epic", "Xbox"...
    public string? CoverImageUrl { get; set; }
    public bool IsInstalled { get; set; } = true;

    /// وسائط سطر أوامر اختيارية عند التشغيل (تحتاجها بعض المنصات مثل Riot وXbox)
    public string? LaunchArguments { get; set; }

    public string StatusText => IsInstalled ? "مثبتة" : "غير مثبتة";
    public bool HasCoverImage => !string.IsNullOrEmpty(CoverImageUrl);

    public override string ToString() => $"{Name} ({Platform})";
}
