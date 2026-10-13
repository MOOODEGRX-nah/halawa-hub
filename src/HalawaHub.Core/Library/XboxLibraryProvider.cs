using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// كشف ألعاب Xbox / Microsoft Store بمزوّد هجين بثلاثة مصادر (نهج الصناعة):
///   أ) حزم مثبّتة: عقد Windows.Games (مطابقة مزدوجة) أو مسار XboxGames أو
///      وجود MicrosoftGame.config بمسار الحزمة — تشغيل عبر shell:appsFolder
///   ب) مسح مجلدات: كل قرص فيه .GamingRoot نفحص XboxGames\*\Content\MicrosoftGame.config
///      (يلتقط ألعاب GDK غير المسجلة بالعقد — الحالة المكتشفة بتقرير تشخيص حقيقي)
///      الاسم الحقيقي يُقرأ من ShellVisuals داخل الـ config، والتشغيل عبر exe داخل Content
///   ج) استبعاد الإطارات/الموارد/الحزم تلقائيًا + إزالة تكرار الاسم بين المصدرين
/// كل مرحلة تسجل أعدادها وعينة أسماء — أي جهاز يشخّص نفسه من السجل.
///
/// تحصين B06:
///   - ReadToEndAsync + WaitForExit(60000) + Kill(entireProcessTree) عند Timeout
///   - Cache successes only (القائمة الفارغة لا تُكاش — فشل عابر لا يخفي المكتبة)
///   - InvalidateCache() public يستدعيه زر التحديث
///   - StandardOutputEncoding = UTF8 + [Console]::OutputEncoding داخل السكربت
///   - مسار powershell.exe مطلق من %SystemRoot%
///   - stderr ملتقط ومُسجَّل
/// </summary>
public class XboxLibraryProvider : IGameLibraryProvider
{
    private static List<AppxEntry>? _cachedEntries;
    private static DateTime _lastScan = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly object CacheLock = new();

    // B06: UTF-8 داخل السكربت + مسار مطلق لـ powershell
    private const string PsScript = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$gameIds = @{}
try {
    $regPath = 'Registry::HKEY_CLASSES_ROOT\Extensions\ContractId\Windows.Games\PackageId'
    if (Test-Path $regPath) {
        Get-ChildItem $regPath -ErrorAction Stop | ForEach-Object { $gameIds[$_.PSChildName] = $true }
    }
} catch { }

$emitted = @{}
$results = @()

foreach ($p in (Get-AppxPackage -ErrorAction SilentlyContinue)) {
    if ($p.IsFramework -or $p.IsResourcePackage -or $p.IsBundle) { continue }
    $cfg = Join-Path $p.InstallLocation 'MicrosoftGame.config'
    $hasCfg = Test-Path -LiteralPath $cfg -ErrorAction SilentlyContinue
    $inContract = $gameIds.ContainsKey($p.PackageFullName) -or $gameIds.ContainsKey($p.PackageFamilyName)
    $inXboxDir = ($p.InstallLocation -like '*\XboxGames\*')
    if (-not ($inContract -or $inXboxDir -or $hasCfg)) { continue }
    $disp = $p.Name
    if ($hasCfg) {
        try {
            [xml]$x = Get-Content -LiteralPath $cfg -Raw -ErrorAction Stop
            if ($x.Game.ShellVisuals.DefaultDisplayName) { $disp = $x.Game.ShellVisuals.DefaultDisplayName }
        } catch { }
    }
    $key = $disp.ToLower()
    if ($emitted.ContainsKey($key)) { continue }
    $emitted[$key] = $true
    $appId = 'App'
    try {
        $manifest = Get-AppxPackageManifest $p.PackageFullName -ErrorAction Stop
        $app = $manifest.Package.Applications.Application | Select-Object -First 1
        if ($app.Id) { $appId = $app.Id }
    } catch { }
    $results += [PSCustomObject]@{
        Name = $disp
        PackageFamilyName = $p.PackageFamilyName
        InstallLocation = $p.InstallLocation
        AppId = $appId
        Source = 'package'
        ExePath = ''
    }
}

foreach ($d in (Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue)) {
    $root = $d.Root
    if (-not (Test-Path (Join-Path $root '.GamingRoot') -ErrorAction SilentlyContinue)) { continue }
    $xg = Join-Path $root 'XboxGames'
    if (-not (Test-Path $xg -ErrorAction SilentlyContinue)) { continue }
    foreach ($g in (Get-ChildItem $xg -Directory -ErrorAction SilentlyContinue)) {
        $cfg = Join-Path $g.FullName 'Content\MicrosoftGame.config'
        if (-not (Test-Path -LiteralPath $cfg -ErrorAction SilentlyContinue)) { continue }
        $disp = $g.Name
        try {
            [xml]$x = Get-Content -LiteralPath $cfg -Raw -ErrorAction Stop
            if ($x.Game.ShellVisuals.DefaultDisplayName) { $disp = $x.Game.ShellVisuals.DefaultDisplayName }
        } catch { }
        $key = $disp.ToLower()
        if ($emitted.ContainsKey($key)) { continue }
        $exe = Get-ChildItem -Path (Join-Path $g.FullName 'Content') -Filter '*.exe' -Recurse -Depth 2 -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $exe) { continue }
        $emitted[$key] = $true
        $results += [PSCustomObject]@{
            Name = $disp
            PackageFamilyName = ''
            InstallLocation = (Join-Path $g.FullName 'Content')
            AppId = ''
            Source = 'xboxgames-dir'
            ExePath = $exe.FullName
        }
    }
}

ConvertTo-Json -InputObject @($results) -Compress -Depth 4
";

    public string PlatformName => "Xbox / Microsoft Store";

    public bool IsAvailable() => OperatingSystem.IsWindows();

    /// <summary>
    /// يبطل الكاش — يُستدعى من زر التحديث حتى تظهر الألعاب المثبتة حديثًا فورًا
    /// بدلاً من الانتظار 5 دقائق.
    /// </summary>
    public static void InvalidateCache()
    {
        lock (CacheLock)
        {
            _cachedEntries = null;
            _lastScan = DateTime.MinValue;
        }
    }

    public IEnumerable<GameInfo> ScanLibrary()
    {
        List<AppxEntry>? cached;
        lock (CacheLock)
        {
            cached = (_cachedEntries != null && DateTime.UtcNow - _lastScan < CacheDuration)
                ? _cachedEntries
                : null;
        }

        if (cached != null)
        {
            foreach (var entry in cached)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                if (string.IsNullOrEmpty(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation)) continue;
                yield return ToGameInfo(entry);
            }
            yield break;
        }

        var contractCount = CountContractKeys();
        Log.Info($"Xbox: عقد Windows.Games فيه {contractCount} مفتاح مسجل");

        List<AppxEntry> entries;
        try
        {
            entries = QueryInstalledPackages();
        }
        catch (Exception ex)
        {
            Log.Error("Xbox: فشل تشغيل سكربت الفحص", ex);
            yield break;
        }

        // B06: cache successes only — القائمة الفارغة (فشل عابر) لا تُكاش
        if (entries.Count > 0)
        {
            lock (CacheLock)
            {
                _cachedEntries = entries;
                _lastScan = DateTime.UtcNow;
            }
        }
        else
        {
            Log.Info("Xbox: النتيجة فارغة — لم تُكاش (سيُعاد الفحص عند الطلب التالي)");
        }

        var pkgCount = entries.Count(e => e.Source == "package");
        var dirCount = entries.Count(e => e.Source == "xboxgames-dir");
        var sample = string.Join(", ", entries.Take(5).Select(e => e.Name));
        Log.Info($"Xbox: تطابق {entries.Count} لعبة (حزم={pkgCount} مجلدات={dirCount}) [{sample}]");

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (string.IsNullOrEmpty(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation)) continue;
            yield return ToGameInfo(entry);
        }
    }

    private static GameInfo ToGameInfo(AppxEntry entry)
    {
        var isPkg = entry.Source == "package";
        var appId = string.IsNullOrEmpty(entry.AppId) ? "App" : entry.AppId;
        return new GameInfo
        {
            Id = isPkg ? entry.PackageFamilyName : "xg|" + entry.InstallLocation,
            Name = entry.Name,
            InstallPath = entry.InstallLocation,
            ExecutablePath = isPkg ? "explorer.exe" : entry.ExePath,
            LaunchArguments = isPkg ? $"shell:appsFolder\\{entry.PackageFamilyName}!{appId}" : "",
            Platform = "Xbox / Microsoft Store",
            IsInstalled = true
        };
    }

    private static int CountContractKeys()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"Extensions\ContractId\Windows.Games\PackageId");
            return key?.SubKeyCount ?? 0;
        }
        catch
        {
            return -1;
        }
    }

    private static List<AppxEntry> QueryInstalledPackages()
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"halawahub_xbox_{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, PsScript, Encoding.UTF8);

        try
        {
            // B06: مسار مطلق لـ PowerShell من %SystemRoot%
            var psExe = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(psExe)) psExe = "powershell.exe"; // fallback

            var psi = new ProcessStartInfo(psExe)
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Log.Error("Xbox: Process.Start returned null");
                return new List<AppxEntry>();
            }

            // B06: قراءة غير متزامنة + WaitForExit قبلها (timeout حقيقي)
            var readOut = process.StandardOutput.ReadToEndAsync();
            var readErr = process.StandardError.ReadToEndAsync();
            var exited = process.WaitForExit(60_000);

            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                Log.Error("Xbox: PowerShell timed out after 60s — killed entire process tree");
                return new List<AppxEntry>();
            }

            var output = readOut.GetAwaiter().GetResult();
            var errorOut = readErr.GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(errorOut))
                Log.Error($"Xbox: PowerShell stderr: {errorOut.Trim()}");

            if (process.ExitCode != 0)
            {
                Log.Error($"Xbox: PowerShell exited with code {process.ExitCode}");
                return new List<AppxEntry>();
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                Log.Info("Xbox: PowerShell produced empty output");
                return new List<AppxEntry>();
            }

            using var doc = JsonDocument.Parse(output);
            var elements = doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { doc.RootElement };

            var results = new List<AppxEntry>();
            foreach (var el in elements)
            {
                results.Add(new AppxEntry(
                    el.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
                    el.TryGetProperty("PackageFamilyName", out var p) ? p.GetString() ?? "" : "",
                    el.TryGetProperty("InstallLocation", out var l) ? l.GetString() ?? "" : "",
                    el.TryGetProperty("AppId", out var a) ? a.GetString() ?? "" : "",
                    el.TryGetProperty("Source", out var s) ? s.GetString() ?? "" : "",
                    el.TryGetProperty("ExePath", out var x) ? x.GetString() ?? "" : ""
                ));
            }
            return results;
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* تجاهل */ }
        }
    }

    private record AppxEntry(string Name, string PackageFamilyName, string InstallLocation, string AppId, string Source, string ExePath);
}
