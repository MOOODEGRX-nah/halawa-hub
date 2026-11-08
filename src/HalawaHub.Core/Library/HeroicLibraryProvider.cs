using System.IO;
using System.Text.Json;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// ألعاب Heroic على لينكس (Epic وGOG وAmazon) من ملفات Heroic نفسها، بدون تشغيله.
/// الصيغ مأخوذة من مصدر Heroic v2.22.4:
///   Epic:   legendaryConfig/legendary/installed.json  { appName: { title, install_path, is_dlc } }
///   GOG:    gog_store/installed.json                  { installed: [ { appName, install_path, is_dlc } ] }
///           والعنوان من store_cache/gog_library.json  { games: [ { app_name, title } ] }
///   Amazon: nile_config/nile/installed.json           [ { id, path } ]
///           والعنوان من nile_config/nile/library.json [ { product: { id, title } } ]
/// التشغيل عبر بروتوكول heroic:// (src/backend/protocol.ts) وصيغة المسار تعمل مع
/// الإصدارات القديمة والجديدة، ويفتحها النظام عبر xdg-open للتثبيت العادي وFlatpak معًا.
/// </summary>
public class HeroicLibraryProvider : IGameLibraryProvider
{
    public const string Platform = "Heroic";

    private readonly IReadOnlyList<string>? _configRoots;

    public HeroicLibraryProvider()
    {
    }

    /// للاختبارات: مجلدات إعداد Heroic مزيفة على القرص
    internal HeroicLibraryProvider(IEnumerable<string> configRoots)
    {
        _configRoots = configRoots.ToList();
    }

    public string PlatformName => Platform;

    public bool IsAvailable() =>
        (_configRoots != null || OperatingSystem.IsLinux()) && ConfigRoots().Any(Directory.Exists);

    public IEnumerable<GameInfo> ScanLibrary()
    {
        var games = new List<GameInfo>();
        foreach (var root in ConfigRoots().Where(Directory.Exists))
        {
            ReadStore("Epic", () => ReadEpic(root), games);
            ReadStore("GOG", () => ReadGog(root), games);
            ReadStore("Amazon", () => ReadAmazon(root), games);
        }
        return games;
    }

    private IReadOnlyList<string> ConfigRoots() => _configRoots ?? DefaultConfigRoots(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal static IReadOnlyList<string> DefaultConfigRoots(string? xdgConfigHome, string home) => new[]
    {
        Path.Combine(string.IsNullOrWhiteSpace(xdgConfigHome) ? Path.Combine(home, ".config") : xdgConfigHome, "heroic"),
        Path.Combine(home, ".var", "app", "com.heroicgameslauncher.hgl", "config", "heroic")
    };

    // متجر بملف تالف لا يمنع قراءة المتاجر الأخرى
    private static void ReadStore(string store, Func<IEnumerable<GameInfo>> read, List<GameInfo> games)
    {
        try
        {
            games.AddRange(read());
        }
        catch (Exception ex)
        {
            Log.Error($"Heroic: could not read the {store} games", ex);
        }
    }

    private static IEnumerable<GameInfo> ReadEpic(string root)
    {
        var path = Path.Combine(root, "legendaryConfig", "legendary", "installed.json");
        if (!File.Exists(path)) yield break;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind != JsonValueKind.Object) yield break;

        foreach (var entry in doc.RootElement.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object || IsDlc(entry.Value)) continue;

            var game = BuildGame("legendary", entry.Name, GetString(entry.Value, "title"), GetString(entry.Value, "install_path"));
            if (game != null) yield return game;
        }
    }

    private static IEnumerable<GameInfo> ReadGog(string root)
    {
        var path = Path.Combine(root, "gog_store", "installed.json");
        if (!File.Exists(path)) yield break;

        var titles = ReadGogTitles(root);

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("installed", out var installed)
            || installed.ValueKind != JsonValueKind.Array) yield break;

        foreach (var entry in installed.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || IsDlc(entry)) continue;

            var appName = GetString(entry, "appName");
            if (string.IsNullOrEmpty(appName)) continue;

            titles.TryGetValue(appName, out var title);
            var game = BuildGame("gog", appName, title, GetString(entry, "install_path"));
            if (game != null) yield return game;
        }
    }

    private static Dictionary<string, string> ReadGogTitles(string root)
    {
        var titles = new Dictionary<string, string>();
        try
        {
            var path = Path.Combine(root, "store_cache", "gog_library.json");
            if (!File.Exists(path)) return titles;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("games", out var games)
                || games.ValueKind != JsonValueKind.Array) return titles;

            foreach (var game in games.EnumerateArray())
            {
                if (game.ValueKind != JsonValueKind.Object) continue;
                var id = GetString(game, "app_name");
                var title = GetString(game, "title");
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title)) titles[id] = title;
            }
        }
        catch (Exception ex)
        {
            // بدون العناوين تظهر الألعاب باسم مجلدها، وهذا أفضل من إخفائها
            Log.Error("Heroic: gog_library.json unreadable", ex);
        }
        return titles;
    }

    private static IEnumerable<GameInfo> ReadAmazon(string root)
    {
        var dir = Path.Combine(root, "nile_config", "nile");
        var path = Path.Combine(dir, "installed.json");
        if (!File.Exists(path)) yield break;

        var titles = ReadAmazonTitles(Path.Combine(dir, "library.json"));

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind != JsonValueKind.Array) yield break;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;

            var id = GetString(entry, "id");
            if (string.IsNullOrEmpty(id)) continue;

            titles.TryGetValue(id, out var title);
            var game = BuildGame("nile", id, title, GetString(entry, "path"));
            if (game != null) yield return game;
        }
    }

    private static Dictionary<string, string> ReadAmazonTitles(string path)
    {
        var titles = new Dictionary<string, string>();
        try
        {
            if (!File.Exists(path)) return titles;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return titles;

            foreach (var game in doc.RootElement.EnumerateArray())
            {
                if (game.ValueKind != JsonValueKind.Object
                    || !game.TryGetProperty("product", out var product)
                    || product.ValueKind != JsonValueKind.Object) continue;

                var id = GetString(product, "id");
                var title = GetString(product, "title");
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title)) titles[id] = title;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Heroic: nile library.json unreadable", ex);
        }
        return titles;
    }

    private static GameInfo? BuildGame(string runner, string appName, string? title, string? installPath)
    {
        if (string.IsNullOrEmpty(installPath)) return null;

        var name = !string.IsNullOrWhiteSpace(title)
            ? title
            : Path.GetFileName(installPath.TrimEnd('/', '\\'));
        if (string.IsNullOrWhiteSpace(name)) return null;

        return new GameInfo
        {
            // المعرّف يشمل المتجر: نفس الرقم قد يتكرر بين متجرين
            Id = $"{runner}:{appName}",
            Name = name,
            InstallPath = installPath,
            ExecutablePath = $"heroic://launch/{runner}/{Uri.EscapeDataString(appName)}",
            Platform = Platform,
            IsInstalled = Directory.Exists(installPath)
        };
    }

    private static bool IsDlc(JsonElement entry) =>
        entry.TryGetProperty("is_dlc", out var dlc) && dlc.ValueKind == JsonValueKind.True;

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
