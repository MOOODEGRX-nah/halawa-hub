using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// كشف ألعاب Xbox / Microsoft Store بالمنهج العام المعروف بالصناعة:
/// لا توجد API ترجع "الألعاب" جاهزة، لذلك نعدّد الحزم المثبتة (مصدر Playnite
/// وshell:AppsFolder) ثم نصنفها بثلاث طبقات:
///   1) عقد Windows.Games بالسجل (نفس مصدر Xbox Game Bar) بمطابقة مزدوجة
///      (PackageFullName أو PackageFamilyName لاختلاف الصيغ بين إصدارات ويندوز)
///   2) مسار التثبيت تحت XboxGames على أي قرص (ألعاب Gaming Services،
///      لأن مجلد WindowsApps محمي من القراءة المباشرة)
///   3) استبعاد الإطارات والحزم النظامية تلقائيًا (IsFramework)
/// كل مرحلة تسجل أعدادها وأسماء عينات منها — أي جهاز يشخّص نفسه من السجل.
/// ملاحظة: المكتبة السحابية (ألعاب غير مثبتة) تتطلب تسجيل دخول Xbox Live
/// (نهج GOG Galaxy) وهي خطوة مستقبلية وليست كشفًا محليًا.
/// </summary>
public class XboxLibraryProvider : IGameLibraryProvider
{
    private const string PsScript = @"
$gameIds = @{}
try {
    $regPath = 'Registry::HKEY_CLASSES_ROOT\Extensions\ContractId\Windows.Games\PackageId'
    if (Test-Path $regPath) {
        Get-ChildItem $regPath -ErrorAction Stop | ForEach-Object { $gameIds[$_.PSChildName] = $true }
    }
} catch { }

$candidates = Get-AppxPackage | Where-Object { -not $_.IsFramework }

$games = $candidates | Where-Object {
    $gameIds.ContainsKey($_.PackageFullName) -or
    $gameIds.ContainsKey($_.PackageFamilyName) -or
    ($_.InstallLocation -like '*\XboxGames\*')
}

$games | ForEach-Object {
    try {
        $manifest = Get-AppxPackageManifest $_.PackageFullName -ErrorAction Stop
        $app = $manifest.Package.Applications.Application | Select-Object -First 1
        [PSCustomObject]@{
            Name = $_.Name
            PackageFamilyName = $_.PackageFamilyName
            InstallLocation = $_.InstallLocation
            AppId = $app.Id
        }
    } catch { }
} | ConvertTo-Json -Compress
";

    public string PlatformName => "Xbox / Microsoft Store";

    public bool IsAvailable() => OperatingSystem.IsWindows();

    public IEnumerable<GameInfo> ScanLibrary()
    {
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

        var sample = string.Join(", ", entries.Take(5).Select(e => e.Name));
        Log.Info($"Xbox: تطابق {entries.Count} حزمة لعبة [{sample}]");

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || string.IsNullOrEmpty(entry.PackageFamilyName))
                continue;

            if (string.IsNullOrEmpty(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation))
                continue;

            var appId = string.IsNullOrEmpty(entry.AppId) ? "App" : entry.AppId;

            yield return new GameInfo
            {
                Id = entry.PackageFamilyName,
                Name = entry.Name,
                InstallPath = entry.InstallLocation,
                ExecutablePath = "explorer.exe",
                LaunchArguments = $"shell:appsFolder\\{entry.PackageFamilyName}!{appId}",
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
            process?.WaitForExit(30000);

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
                    el.TryGetProperty("AppId", out var a) ? a.GetString() ?? "" : ""
                ));
            }
            return results;
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { /* تجاهل */ }
        }
    }

    private record AppxEntry(string Name, string PackageFamilyName, string InstallLocation, string AppId);
}
