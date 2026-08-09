using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;

namespace HalawaHub.Core.Library;

/// <summary>
/// يكتشف ألعاب Xbox / Microsoft Store المثبتة عبر استعلام PowerShell (Get-AppxPackage).
///
/// ملاحظة مهمة: مايكروسوفت ما توفر API رسمي يرجع لك "الألعاب فقط" من بين كل
/// تطبيقات UWP المثبتة على جهازك، فهذا الكاشف يستخدم قائمة استبعاد تقريبية
/// لأدوات النظام المعروفة (Notepad، الآلة الحاسبة، إلخ) وبيعرض الباقي كمرشحين.
/// قد تحتاج تضيف لقائمة الاستبعاد لاحقًا لو ظهرت لك تطبيقات مو ألعاب.
/// </summary>
public class XboxLibraryProvider : IGameLibraryProvider
{
    private static readonly string[] ExcludedPrefixes =
    {
        "Microsoft.Windows", "Microsoft.Xbox.TCUI", "Microsoft.XboxGameCallableUI",
        "Microsoft.XboxIdentityProvider", "Microsoft.XboxSpeechToTextOverlay",
        "Microsoft.GamingApp", "Microsoft.549981C3F5F10", "Microsoft.AAD",
        "Microsoft.Advertising", "Microsoft.AsyncTextService", "Microsoft.BingWeather",
        "Microsoft.MicrosoftEdge", "Microsoft.MicrosoftOfficeHub", "Microsoft.MicrosoftStickyNotes",
        "Microsoft.People", "Microsoft.ScreenSketch", "Microsoft.SecHealthUI", "Microsoft.Todos",
        "Microsoft.Windows.Photos", "Microsoft.WindowsAlarms", "Microsoft.WindowsCalculator",
        "Microsoft.WindowsCamera", "Microsoft.WindowsCommunicationsapps", "Microsoft.WindowsFeedbackHub",
        "Microsoft.WindowsMaps", "Microsoft.WindowsNotepad", "Microsoft.WindowsSoundRecorder",
        "Microsoft.WindowsStore", "Microsoft.WindowsTerminal", "Microsoft.YourPhone",
        "Microsoft.ZuneMusic", "Microsoft.ZuneVideo", "MicrosoftCorporationII.QuickAssist",
        "Microsoft.OutlookForWindows", "Microsoft.VCLibs", "Microsoft.NET", "Microsoft.UI.Xaml",
        "Clipchamp.Clipchamp",
    };

    private const string PsScript = @"
Get-AppxPackage | Where-Object { -not $_.IsFramework } | ForEach-Object {
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
        List<AppxEntry> entries;
        try
        {
            entries = QueryInstalledPackages();
        }
        catch
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || string.IsNullOrEmpty(entry.PackageFamilyName))
                continue;

            if (ExcludedPrefixes.Any(p => entry.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (string.IsNullOrEmpty(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation))
                continue;

            var appId = string.IsNullOrEmpty(entry.AppId) ? "App" : entry.AppId;

            yield return new GameInfo
            {
                Id = entry.PackageFamilyName,
                Name = entry.Name,
                InstallPath = entry.InstallLocation,
                // أسلوب موثّق لتشغيل تطبيقات UWP من سطر الأوامر عبر مستكشف الملفات
                ExecutablePath = "explorer.exe",
                LaunchArguments = $"shell:appsFolder\\{entry.PackageFamilyName}!{appId}",
                Platform = "Xbox / Microsoft Store",
                IsInstalled = true
            };
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
            process?.WaitForExit(15000);

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
