using System.IO;
using System.Diagnostics;
using System.Linq;
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
/// </summary>
public class XboxLibraryProvider : IGameLibraryProvider
    private static List<AppxEntry>? _cachedEntries;
    private static DateTime _lastScan = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
{
    private const string PsScript = @"
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

    public IEnumerable<GameInfo> ScanLibrary()
    {

        // Cache check — تجنب فحص PowerShell كل مرة
        if (_cachedEntries != null && DateTime.UtcNow - _lastScan < CacheDuration)
        {
            Log.Info($"Xbox: cache hit ({_cachedEntries.Count} لعبة)");
            foreach (var entry in _cachedEntries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                if (string.IsNullOrEmpty(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation)) continue;
                var isPkg = entry.Source == "package";
                var appId = string.IsNullOrEmpty(entry.AppId) ? "App" : entry.AppId;
                yield return new GameInfo
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
            yield break;
        }
        var contractCount = CountContractKeys();
        Log.Info($"Xbox: عقد Windows.Games فيه {contractCount} مفتاح مسجل");

        List<AppxEntry> entries;
        try
        {
            entries = QueryInstalledPackages();
        }
        catch
        {
            Log.Error("Xbox: فشل تشغيل سكربت الفحص");
            yield break;
        }

        var pkgCount = entries.Count(e => e.Source == "package");
        var dirCount = entries.Count(e => e.Source == "xboxgames-dir");
        var sample = string.Join(", ", entries.Take(5).Select(e => e.Name));
        Log.Info($"Xbox: تطابق {entries.Count} لعبة (حزم={pkgCount} مجلدات={dirCount}) [{sample}]");

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (string.IsNullOrEmpty(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation)) continue;

            var isPkg = entry.Source == "package";
            var appId = string.IsNullOrEmpty(entry.AppId) ? "App" : entry.AppId;

            yield return new GameInfo
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
        File.WriteAllText(scriptPath, PsScript);

        try
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd() ?? "";
            process?.WaitForExit(60000);

            if (string.IsNullOrWhiteSpace(output)) return new List<AppxEntry>();

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
