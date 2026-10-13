using System.IO;
using System.Text.Json;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// يكتشف ألعاب Epic Games المثبتة عبر قراءة ملفات المانيفست (.item بصيغة JSON)
/// اللي يحفظها Epic Games Launcher مباشرة على القرص، بدون أي API أو اتصال إنترنت.
/// </summary>
public class EpicLibraryProvider : IGameLibraryProvider
{
    private static readonly string ManifestsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Manifests");

    public string PlatformName => "Epic Games";

    public bool IsAvailable() => Directory.Exists(ManifestsPath);

    public IEnumerable<GameInfo> ScanLibrary()
    {
        if (!Directory.Exists(ManifestsPath)) yield break;

        string[] itemFiles;
        try
        {
            itemFiles = Directory.GetFiles(ManifestsPath, "*.item");
        }
        catch (Exception ex)
        {
            Log.Error($"Epic: cannot enumerate {ManifestsPath}", ex);
            yield break;
        }

        foreach (var itemFile in itemFiles)
        {
            GameInfo? game = null;
            try
            {
                var json = File.ReadAllText(itemFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var displayName = root.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : null;
                var installLocation = root.TryGetProperty("InstallLocation", out var il) ? il.GetString() : null;
                var appName = root.TryGetProperty("AppName", out var an) ? an.GetString() : null;

                if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(installLocation))
                    continue;

                game = new GameInfo
                {
                    Id = appName ?? displayName,
                    Name = displayName,
                    InstallPath = installLocation,
                    // بروتوكول Epic المسجّل بالنظام، يفتحه Windows تلقائيًا عبر UseShellExecute
                    ExecutablePath = string.IsNullOrEmpty(appName)
                        ? installLocation
                        : $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true",
                    Platform = "Epic Games",
                    IsInstalled = Directory.Exists(installLocation)
                };
            }
            catch (Exception ex)
            {
                // ملف مانيفست تالف أو بصيغة غير متوقعة، تجاهله وكمّل للي بعده
                Log.Error($"Epic: manifest failed {Path.GetFileName(itemFile)}", ex);
            }

            if (game != null) yield return game;
        }
    }
}
