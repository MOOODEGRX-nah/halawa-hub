namespace HalawaHub.Core.Library;

/// ما يستطيع Halawa رؤيته لكل منصة (للعرض الصادق في الإعدادات > المنصات)
public sealed record PlatformCapabilities(bool InstalledGames, bool PlayTime, bool Updates, bool RepairOnly, bool RunningOutside);

public static class PlatformCatalog
{
    /// <summary>
    /// PlayTime: متتبع حقيقي (سجل Steam أو مجلد اللعبة). Updates: كشف "تحديث متوفر" من
    /// ملفات محلية (Steam فقط؛ Epic يكشف حاجة الإصلاح فقط). RunningOutside: يعرف أن اللعبة
    /// تعمل حتى لو شُغّلت من خارج Halawa (Steam عبر سجل ويندوز/registry.vdf).
    /// </summary>
    public static PlatformCapabilities For(string platform) => platform switch
    {
        "Steam" => new(true, true, true, false, true),
        "Epic Games" => new(true, true, false, true, false),
        "GOG" or "Riot Games" or "Xbox / Microsoft Store" => new(true, true, false, false, false),
        "Heroic" or "Lutris" => new(true, true, false, false, false),
        ManualGames.Platform => new(true, true, false, false, false),
        _ => new(true, false, false, false, false)
    };

    /// المسار الذي وجد Halawa المنصة فيه، أو وصف مصدرها؛ null إن لم تُكتشف
    public static string? DetectedPath(string platform) => platform switch
    {
        "Steam" => SteamLibraryProvider.GetSteamPath(),
        "Epic Games" => Directory.Exists(EpicLibraryProvider.ManifestsPath) ? EpicLibraryProvider.ManifestsPath : null,
        "Riot Games" => Directory.Exists(RiotLibraryProvider.MetadataPath) ? RiotLibraryProvider.MetadataPath : null,
        "GOG" => @"HKLM\SOFTWARE\GOG.com\Games",
        "Xbox / Microsoft Store" => "Windows.Games / Get-AppxPackage",
        ManualGames.Platform => ManualGames.DefaultPath,
        _ => null
    };
}
