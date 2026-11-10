using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

namespace HalawaHub.Core.Plugins;

public enum PluginState
{
    /// محمّلة وتعمل
    Enabled,

    /// أوقفها المستخدم (أو رفضها)
    Disabled,

    /// جديدة أو تغيّر ملفها: لا تُحمَّل حتى يوافق المستخدم
    NeedsApproval,

    /// وافق عليها المستخدم لكن تحميلها فشل
    Failed
}

/// إضافة واحدة كما تظهر في الإعدادات
public sealed class PluginEntry
{
    public required string FileName { get; init; }
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
    public PluginState State { get; internal set; }

    /// كانت موافقًا عليها بملف آخر — أي أن الملف تغيّر
    public bool Changed { get; internal set; }

    /// من نفس بناء البرنامج (إضافة المثال التي تأتي مع الإصدار): موثوقة تلقائيًا
    public bool Bundled { get; internal set; }

    public string? Error { get; internal set; }

    internal List<IGameLibraryProvider> Providers { get; } = new();
    internal List<IGameTool> Tools { get; } = new();
}

/// سجل الإضافات الموافق عليها (plugins.json في مجلد البيانات)
public sealed class PluginTrustRecord
{
    public string Sha256 { get; set; } = "";
    public bool Enabled { get; set; }
    public string Path { get; set; } = "";
}

public static class PluginTrustStore
{
    public static string DefaultPath => AppPaths.InData("plugins.json");

    public static Dictionary<string, PluginTrustRecord> Load(string path) =>
        AtomicFile.ReadWithBackup(path, json =>
            JsonSerializer.Deserialize<Dictionary<string, PluginTrustRecord>>(json), "plugins.json") is { } d
            ? new Dictionary<string, PluginTrustRecord>(d, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, PluginTrustRecord>(StringComparer.OrdinalIgnoreCase);

    public static void Save(string path, Dictionary<string, PluginTrustRecord> records)
    {
        try
        {
            AtomicFile.WriteAllText(path, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log.Error("plugins.json: save failed", ex);
        }
    }
}

/// <summary>
/// يفحص مجلد "Plugins" ويكتشف أي Class يطبّق IGameLibraryProvider أو IGameTool.
/// أمان: ملف DLL جديد لا يُحمَّل (لا يُنفَّذ منه شيء) قبل موافقة المستخدم على اسمه
/// ومساره وبصمته SHA256؛ والموافقة مرتبطة بالبصمة، فأي تعديل على الملف يعيد السؤال.
/// الاستثناء الوحيد: إضافة من نفس بناء البرنامج (الاسم والإصدار نفسهما) — ملف بجانب
/// البرنامج له نفس ثقة ملفات البرنامج نفسها، والسؤال عنها مع كل تحديث بلا فائدة.
/// </summary>
public class PluginLoader
{
    public const string BundledPluginFile = "HalawaHub.Plugins.Sample.dll";

    private readonly string _pluginsDirectory;
    private readonly string _trustPath;
    private readonly string _appVersion;
    private Dictionary<string, PluginTrustRecord> _trust = new(StringComparer.OrdinalIgnoreCase);

    public List<IGameLibraryProvider> LibraryProviders { get; } = new();
    public List<IGameTool> GameTools { get; } = new();
    public List<PluginEntry> Plugins { get; } = new();

    public PluginLoader(string pluginsDirectory, string? trustPath = null, string? appVersion = null)
    {
        _pluginsDirectory = pluginsDirectory;
        _trustPath = trustPath ?? PluginTrustStore.DefaultPath;
        _appVersion = appVersion ?? AppInfo.Version;
    }

    public void LoadPlugins()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            try { Directory.CreateDirectory(_pluginsDirectory); }
            catch (Exception ex) { Log.Error("could not create the Plugins folder", ex); }
            return;
        }

        _trust = PluginTrustStore.Load(_trustPath);

        foreach (var dllPath in Directory.GetFiles(_pluginsDirectory, "*.dll").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string sha;
            try
            {
                sha = HashFile(dllPath);
            }
            catch (Exception ex)
            {
                Log.Error($"plugin unreadable: {dllPath}", ex);
                continue;
            }

            var entry = new PluginEntry { FileName = System.IO.Path.GetFileName(dllPath), Path = dllPath, Sha256 = sha };
            Plugins.Add(entry);

            if (_trust.TryGetValue(entry.FileName, out var rec) && string.Equals(rec.Sha256, sha, StringComparison.OrdinalIgnoreCase))
            {
                entry.State = rec.Enabled ? PluginState.Enabled : PluginState.Disabled;
            }
            else if (IsBundled(dllPath))
            {
                entry.Bundled = true;
                // الإصدار السابق من نفس الإضافة: نحترم إيقاف المستخدم لها
                entry.State = rec is { Enabled: false } ? PluginState.Disabled : PluginState.Enabled;
                _trust[entry.FileName] = new PluginTrustRecord { Sha256 = sha, Enabled = entry.State == PluginState.Enabled, Path = dllPath };
                PluginTrustStore.Save(_trustPath, _trust);
            }
            else
            {
                entry.State = PluginState.NeedsApproval;
                entry.Changed = rec != null;
                Log.Info($"plugin waiting for approval: {entry.FileName} sha256={sha}{(entry.Changed ? " (file changed)" : "")}");
            }

            if (entry.State == PluginState.Enabled) Activate(entry);
        }
    }

    public IEnumerable<PluginEntry> PendingApproval => Plugins.Where(p => p.State == PluginState.NeedsApproval);

    /// موافقة أو تشغيل من الإعدادات: يُحمَّل الآن، وتُحفظ البصمة
    public bool Enable(PluginEntry entry)
    {
        // الملف قد يتغير بين السؤال والموافقة: الموافقة على البصمة التي رآها المستخدم فقط
        string current;
        try { current = HashFile(entry.Path); }
        catch (Exception ex) { Log.Error($"plugin unreadable: {entry.Path}", ex); return false; }
        if (!string.Equals(current, entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            Log.Error($"plugin {entry.FileName} changed after it was shown to the user — not loading");
            return false;
        }

        _trust[entry.FileName] = new PluginTrustRecord { Sha256 = entry.Sha256, Enabled = true, Path = entry.Path };
        PluginTrustStore.Save(_trustPath, _trust);
        entry.Changed = false;
        if (entry.Providers.Count == 0 && entry.Tools.Count == 0) Activate(entry);
        else
        {
            entry.State = PluginState.Enabled;
            LibraryProviders.AddRange(entry.Providers);
            GameTools.AddRange(entry.Tools);
        }
        return entry.State == PluginState.Enabled;
    }

    /// رفض أو إيقاف: لا تعمل حتى يشغّلها المستخدم من الإعدادات (أو يتغير ملفها فنسأل من جديد)
    public void Disable(PluginEntry entry)
    {
        _trust[entry.FileName] = new PluginTrustRecord { Sha256 = entry.Sha256, Enabled = false, Path = entry.Path };
        PluginTrustStore.Save(_trustPath, _trust);
        foreach (var p in entry.Providers) LibraryProviders.Remove(p);
        foreach (var t in entry.Tools) GameTools.Remove(t);
        entry.State = PluginState.Disabled;
        entry.Changed = false;
    }

    private void Activate(PluginEntry entry)
    {
        try
        {
            var loadContext = new PluginLoadContext(entry.Path);
            var assembly = loadContext.LoadFromAssemblyPath(entry.Path);

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsInterface || type.IsAbstract) continue;

                if (typeof(IGameLibraryProvider).IsAssignableFrom(type) && Activator.CreateInstance(type) is IGameLibraryProvider provider)
                    entry.Providers.Add(provider);

                if (typeof(IGameTool).IsAssignableFrom(type) && Activator.CreateInstance(type) is IGameTool tool)
                    entry.Tools.Add(tool);
            }

            LibraryProviders.AddRange(entry.Providers);
            GameTools.AddRange(entry.Tools);
            entry.State = PluginState.Enabled;
            entry.Error = null;
        }
        catch (Exception ex)
        {
            entry.State = PluginState.Failed;
            entry.Error = ex.Message;
            Log.Error($"failed to load plugin {entry.FileName}", ex);
        }
    }

    /// إضافة المثال من نفس بناء البرنامج: اسم الملف واسم التجميع وإصداره نفس البرنامج.
    /// يُقرأ الإصدار من البيانات الوصفية بلا تحميل الملف في البرنامج.
    internal bool IsBundled(string dllPath)
    {
        if (!string.Equals(System.IO.Path.GetFileName(dllPath), BundledPluginFile, StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var name = AssemblyName.GetAssemblyName(dllPath);
            return name.Name == System.IO.Path.GetFileNameWithoutExtension(BundledPluginFile)
                   && name.Version != null
                   && VersionMatches(name.Version, _appVersion);
        }
        catch
        {
            return false;
        }
    }

    internal static bool VersionMatches(Version assemblyVersion, string appVersion) =>
        Version.TryParse(appVersion, out var app)
        && assemblyVersion.Major == app.Major && assemblyVersion.Minor == app.Minor
        && assemblyVersion.Build == app.Build && Math.Max(assemblyVersion.Revision, 0) == Math.Max(app.Revision, 0);

    internal static string HashFile(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
    }
}

/// سياق تحميل معزول لكل Plugin، يحل تبعياته الخاصة بشكل مستقل
internal class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }
}
