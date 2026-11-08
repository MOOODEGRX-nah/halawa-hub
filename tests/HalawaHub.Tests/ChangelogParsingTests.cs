using FluentAssertions;
using HalawaHub.Core.Updates;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// انحدار: فاصلة زائدة قبل ] كانت ترمي استثناء فلا تفتح نافذة "ما الجديد" أبدًا.
/// </summary>
public class ChangelogParsingTests
{
    [Fact]
    public void Parse_TrailingCommaBeforeBracket_StillWorks()
    {
        const string json = """
        {
          "1.0.0.1": [
            "first",
          ],
          "1.0.0.0": [
            "old"
          ],
        }
        """;

        var parsed = ChangelogService.ParseChangelog(json);

        parsed.Should().NotBeNull();
        parsed!["1.0.0.1"].Should().Equal("first");
        parsed["1.0.0.0"].Should().Equal("old");
    }

    [Fact]
    public void Parse_Comments_AreSkipped()
    {
        const string json = """
        {
          // ملاحظة
          "1.0.0.1": [ "first" ]
        }
        """;

        ChangelogService.ParseChangelog(json)!["1.0.0.1"].Should().Equal("first");
    }

    [Fact]
    public void RepoChangelog_IsStrictlyValidJson_WithoutTrailingCommas()
    {
        // ملف المستودع نفسه يبقى JSON صارم: التسامح في القارئ شبكة أمان فقط
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CHANGELOG.json")))
            dir = dir.Parent;
        dir.Should().NotBeNull();

        var text = File.ReadAllText(Path.Combine(dir!.FullName, "CHANGELOG.json"));

        var act = () => System.Text.Json.JsonDocument.Parse(text).Dispose();
        act.Should().NotThrow();
    }
}
