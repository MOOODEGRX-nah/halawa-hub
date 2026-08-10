using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// يكتشف ألعاب Riot المثبتة (League of Legends, VALORANT, Legends of Runeterra)
/// عبر قراءة ملفات الإعدادات في مجلد Metadata، ويشغّلها عبر RiotClientServices.exe
/// لأن كل ألعاب Riot أصبحت تعمل حصريًا من خلال العميل الموحّد.
/// </summary>
public class RiotLibraryProvider : IGameLibraryProvider
{
    private static readonly string ProgramData =
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static readonly string MetadataPath = Path.Combine(ProgramData, "Riot Games", "Metadata");
    private static readonly string InstallsJsonPath = Path.Combine(ProgramData, "Riot Games", "RiotClientInstalls.json");

    // معرف المنتج الداخلي كما تسميه Riot -> الاسم المعروض بالواجهة
    private static readonly Dictionary<string, string> KnownProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        { "league_of_legends", "League of Legends" },
        { "valorant", "VALORANT" },
        { "bacon", "Legends of Runeterra" }, // الاسم الداخلي التاريخي لـ LoR عند Riot
    };

    public string PlatformName => "Riot Games";

    public bool IsAvailable() => Directory.Exists(MetadataPath);

    public IEnumerable<GameInfo> ScanLibrary()
    {
        if (!Directory.Exists(MetadataPath)) yield break;

        var riotClientPath = GetRiotClientPath();

        foreach (var productDir in Directory.GetDirectories(MetadataPath, "*.live"))
        {
            var folderName = Path.GetFileName(productDir);
            var productId = folderName.Replace(".live", "");
            var settingsFile = Path.Combine(productDir, $"{folderName}.product_settings.yaml");
            if (!File.Exists(settingsFile)) continue;

            var installPath = ReadYamlValue(settingsFile, "product_install_full_path");
            if (string.IsNullOrEmpty(installPath)) continue;

            var displayName = KnownProducts.TryGetValue(productId, out var known) ? known : productId;

            yield return new GameInfo
            {
                Id = productId,
                Name = displayName,
                InstallPath = installPath,
                ExecutablePath = riotClientPath ?? "",
                LaunchArguments = $"--launch-product={productId} --launch-patchline=live",
                Platform = "Riot Games",
                IsInstalled = Directory.Exists(installPath) && riotClientPath != null
            };
        }
    }

    private static string? GetRiotClientPath()
    {
        try
        {
            var json = File.ReadAllText(InstallsJsonPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("rc_default", out var rc))
                return rc.GetString();
        }
        catch
        {
            // ملف مفقود أو تالف
        }
        return null;
    }

    private static string? ReadYamlValue(string yamlPath, string key)
    {
        foreach (var line in File.ReadLines(yamlPath))
        {
            var match = Regex.Match(line, $@"^\s*{key}:\s*""?([^""\r\n]+)""?\s*$");
            if (match.Success) return match.Groups[1].Value.Trim();
        }
        return null;
    }
}
