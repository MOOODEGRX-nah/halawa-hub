using System.IO;
using Microsoft.Win32;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// يكتشف ألعاب GOG المثبتة عبر قراءة سجل الويندوز
/// (GOG يسجّل كل لعبة مثبتة تحت SOFTWARE\GOG.com\Games).
/// </summary>
public class GogLibraryProvider : IGameLibraryProvider
{
    private const string RegPath64 = @"SOFTWARE\WOW6432Node\GOG.com\Games";
    private const string RegPath32 = @"SOFTWARE\GOG.com\Games";

    public string PlatformName => "GOG";

    public bool IsAvailable()
    {
        // إصلاح تسريب RegistryKey: using يضمن dispose فوري
        using var key = OpenGamesKey();
        return key != null;
    }

    public IEnumerable<GameInfo> ScanLibrary()
    {
        using var gamesKey = OpenGamesKey();
        if (gamesKey == null) yield break;

        string[] subKeyNames;
        try
        {
            subKeyNames = gamesKey.GetSubKeyNames();
        }
        catch (Exception ex)
        {
            Log.Error("GOG: cannot enumerate subkeys", ex);
            yield break;
        }

        foreach (var gameId in subKeyNames)
        {
            using var gameKey = gamesKey.OpenSubKey(gameId);
            if (gameKey == null) continue;

            string? name = null;
            string? path = null;
            string? exe = null;
            try
            {
                name = gameKey.GetValue("gameName") as string;
                path = gameKey.GetValue("path") as string;
                exe = gameKey.GetValue("exe") as string;
            }
            catch (Exception ex)
            {
                // subkey تالف أو غير قابل للقراءة — نتخطاه
                Log.Error($"GOG: subkey unreadable {gameId}", ex);
                continue;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;

            yield return new GameInfo
            {
                Id = gameId,
                Name = name,
                InstallPath = path,
                ExecutablePath = string.IsNullOrEmpty(exe) ? path : Path.Combine(path, exe),
                Platform = "GOG",
                IsInstalled = Directory.Exists(path)
            };
        }
    }

    private static RegistryKey? OpenGamesKey()
    {
        try
        {
            return Registry.LocalMachine.OpenSubKey(RegPath64)
                   ?? Registry.LocalMachine.OpenSubKey(RegPath32);
        }
        catch
        {
            return null;
        }
    }
}
