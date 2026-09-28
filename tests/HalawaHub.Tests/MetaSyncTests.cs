using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// تمنع رجوع E09 (حلقة التحديث) و E10 (مفاتيح CHANGELOG الفاضية).
/// تقرأ الملفات مباشرة — صفر اعتماد على كود التطبيق.
/// </summary>
public class MetaSyncTests
{
    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "Directory.Build.props")))
            d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static string GetPropsVersion()
    {
        var root = FindRepoRoot();
        var doc = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        return doc.Descendants("Version").First().Value.Trim();
    }

    [Fact]
    public void VersionFile_MatchesProps()
    {
        var root = FindRepoRoot();
        var props = GetPropsVersion();
        var file = File.ReadAllText(Path.Combine(root, "VERSION")).Trim();
        Assert.Equal(props, file);
    }

    [Fact]
    public void ChangelogTopEntry_MatchesProps()
    {
        var root = FindRepoRoot();
        var props = GetPropsVersion();
        var json = File.ReadAllText(Path.Combine(root, "CHANGELOG.json"));
        using var doc = JsonDocument.Parse(json);
        
        // البنية الفعلية: {"0.0.10.37": [...], "0.0.10.36": [...], ...}
        var firstKey = doc.RootElement.EnumerateObject().First().Name;
        Assert.Equal(props, firstKey);
    }

    [Fact]
    public void ChangelogKeys_HaveNoTrailingSpaces()
    {
        var root = FindRepoRoot();
        var json = File.ReadAllText(Path.Combine(root, "CHANGELOG.json"));
        using var doc = JsonDocument.Parse(json);
        
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            Assert.DoesNotContain(" ", prop.Name); // لا مسافات بالمفاتيح
            Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", prop.Name); // صيغة صحيحة
        }
    }

    [Fact]
    public void Readme_MentionsCurrentVersion()
    {
        var root = FindRepoRoot();
        var props = GetPropsVersion();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Assert.Contains($"v{props} (Beta)", readme);
    }
}
