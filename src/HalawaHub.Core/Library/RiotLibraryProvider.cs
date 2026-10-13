using System.IO;
using System.Text.RegularExpressions;
using System.Text.Json;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

public class RiotLibraryProvider : IGameLibraryProvider
{
    private static readonly string MetadataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Riot Games", "Metadata");

    private static readonly string InstallsJsonPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Riot Games", "RiotClient", "installs.json");

    private static readonly Dictionary<string, string> KnownProducts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["valorant.live"] = "VALORANT",
        ["league_of_legends.live"] = "League of Legends",
        ["legendsofruneterra.live"] = "Legends of Runeterra",
        ["bacon.live"] = "Teamfight Tactics",
        ["lolesports.live"] = "LoL Esports"
    };

    public string PlatformName => "Riot Games";

    public bool IsAvailable() => Directory.Exists(MetadataPath);

    public IEnumerable<GameInfo> ScanLibrary()
    {
        if (!Directory.Exists(MetadataPath)) yield break;

        var riotClientPath = GetRiotClientPath();

        string[] productDirs;
        try
        {
            productDirs = Directory.GetDirectories(MetadataPath, "*.live");
        }
        catch (Exception ex)
        {
            Log.Error($"Riot: cannot enumerate {MetadataPath}", ex);
            yield break;
        }

        foreach (var productDir in productDirs)
        {
            var folderName = Path.GetFileName(productDir);
            var productId = folderName.Replace(".live", "");

            // نعرض بس منتجات Riot الفعلية المعروفة — بعض مجلدات Metadata تخص
            // منتجات داخلية/تجريبية عند Riot (زي "lion") مو ألعاب حقيقية
            if (!KnownProducts.TryGetValue(productId, out var displayName)) continue;

            var settingsFile = Path.Combine(productDir, $"{folderName}.product_settings.yaml");
            if (!File.Exists(settingsFile)) continue;

            string? installPath = null;
            try
            {
                installPath = ReadYamlValue(settingsFile, "product_install_full_path");
            }
            catch (Exception ex)
            {
                Log.Error($"Riot: yaml unreadable {settingsFile}", ex);
                continue;
            }

            if (string.IsNullOrEmpty(installPath)) continue;

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
        catch (Exception ex)
        {
            Log.Error("Riot: installs.json unreadable", ex);
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
