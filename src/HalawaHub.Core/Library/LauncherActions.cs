using HalawaHub.Core.Models;
using Microsoft.Win32;

namespace HalawaHub.Core.Library;

/// أمر يُفتح عبر UseShellExecute: رابط (steam://…) أو ملف تنفيذي مع وسائطه
public sealed record LauncherCommand(string Target, string? Arguments = null);

/// <summary>
/// ما يستطيع Halawa فعله لكل منصة من خارجها، بصدق: لا أحد من المشغّلات يوفّر أمرًا
/// موثّقًا "حدّث الآن". روابط Steam من صفحة steam(6): nav/games/details و validate و uninstall.
/// Xbox/Store: ms-windows-store://downloadsandupdates (موثّق من مايكروسوفت).
/// إجراء غير متاح يرجع null فتُخفى أزراره.
/// </summary>
public sealed class LauncherActions
{
    public Func<string, bool> FileExists { get; init; } = File.Exists;
    public Func<string?> EpicLauncherExe { get; init; } = FindEpicLauncher;
    public Func<string?> GogGalaxyExe { get; init; } = FindGogGalaxy;

    /// الملف التنفيذي المسجَّل لبروتوكول مشغّل (uplay, origin2, rockstar)
    public Func<string, string?> ProtocolExe { get; init; } = FindProtocolExe;
    public bool Windows { get; init; } = OperatingSystem.IsWindows();

    public static LauncherActions System { get; } = new();

    /// اسم المشغّل كما يظهر في "افتح في …"
    public static string LauncherName(string platform) => platform switch
    {
        "Epic Games" => "Epic Games",
        "GOG" => "GOG Galaxy",
        "Riot Games" => "Riot Client",
        "Xbox / Microsoft Store" => "Microsoft Store",
        "Ubisoft" => "Ubisoft Connect",
        "EA" => "EA app",
        "Rockstar" => "Rockstar Games Launcher",
        _ => platform
    };

    /// يفتح اللعبة (أو صفحة التحديثات) في مشغّلها، حيث يوجد زر التحديث الحقيقي
    public LauncherCommand? OpenInLauncher(GameInfo game)
    {
        switch (game.Platform)
        {
            case "Steam" when SteamSessionTracker.IsValidAppId(game.Id):
                return new LauncherCommand($"steam://nav/games/details/{game.Id}");
            case "Epic Games" when Windows && EpicLauncherExe() is { } epic && FileExists(epic):
                return new LauncherCommand(epic);
            case "GOG" when Windows && GogGalaxyExe() is { } galaxy && FileExists(galaxy):
                return new LauncherCommand(galaxy);
            case "Riot Games" when !string.IsNullOrEmpty(game.ExecutablePath) && FileExists(game.ExecutablePath):
                return new LauncherCommand(game.ExecutablePath);
            case "Ubisoft" when Windows && ProtocolExe("uplay") is { } ubi && FileExists(ubi):
                return new LauncherCommand(ubi);
            case "EA" when Windows && ProtocolExe("origin2") is { } ea && FileExists(ea):
                return new LauncherCommand(ea);
            case "Rockstar" when Windows && ProtocolExe("rockstar") is { } rs && FileExists(rs):
                return new LauncherCommand(rs);
            case "Xbox / Microsoft Store" when Windows:
                return new LauncherCommand("ms-windows-store://downloadsandupdates");
            default:
                return null;
        }
    }

    /// فحص الملفات: Steam فقط يوفّر رابطًا لذلك
    public LauncherCommand? Verify(GameInfo game) =>
        game.Platform == "Steam" && SteamSessionTracker.IsValidAppId(game.Id)
            ? new LauncherCommand($"steam://validate/{game.Id}")
            : null;

    /// <summary>
    /// إلغاء التثبيت بأداة المنصة نفسها (تعرض تأكيدها الخاص؛ Halawa لا يحذف ملفات بنفسه).
    /// GOG: برنامج الإزالة unins000.exe الذي يضعه مثبّت GOG (Inno Setup) في مجلد اللعبة.
    /// Epic: لا رابط موثّق للإزالة، فيُفتح المشغّل (UninstallOpensLauncher = true).
    /// </summary>
    public (LauncherCommand Command, bool OpensLauncher)? Uninstall(GameInfo game)
    {
        switch (game.Platform)
        {
            case "Steam" when SteamSessionTracker.IsValidAppId(game.Id):
                return (new LauncherCommand($"steam://uninstall/{game.Id}"), false);
            case "GOG" when !string.IsNullOrEmpty(game.InstallPath):
                var uninstaller = Path.Combine(game.InstallPath, "unins000.exe");
                return FileExists(uninstaller) ? (new LauncherCommand(uninstaller), false) : null;
            case "Epic Games" when OpenInLauncher(game) is { } open:
                return (open, true);
            default:
                return null;
        }
    }

    /// المسار من أمر بروتوكول Epic المسجّل: "C:\…\EpicGamesLauncher.exe" %1
    internal static string? ExeFromCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
        var space = command.IndexOf(' ');
        return space > 0 ? command[..space] : command;
    }

    private static string? FindProtocolExe(string scheme)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(scheme + @"shellopenmmand");
            return ExeFromCommand(key?.GetValue(null) as string);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindEpicLauncher()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(@"com.epicgames.launcher\shell\open\command");
            return ExeFromCommand(key?.GetValue(null) as string);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindGogGalaxy()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths")
                            ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GOG.com\GalaxyClient\paths");
            var dir = key?.GetValue("client") as string;
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "GalaxyClient.exe");
        }
        catch
        {
            return null;
        }
    }
}
