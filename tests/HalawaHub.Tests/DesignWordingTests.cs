using System.Text.RegularExpressions;
using FluentAssertions;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>تحديث التصميم، المرحلة 1: نفس الكلمات في كل مكان وباللغتين.</summary>
public class DesignWordingTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HalawaHub.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("HalawaHub.sln");
    }

    [Fact]
    public void Every_text_exists_in_both_languages()
    {
        Loc.ArabicStrings.Keys.Except(Loc.EnglishStrings.Keys).Should().BeEmpty("every Arabic key needs an English text");
        Loc.EnglishStrings.Keys.Except(Loc.ArabicStrings.Keys).Should().BeEmpty("every English key needs an Arabic text");
    }

    [Fact]
    public void Placeholders_match_between_languages()
    {
        static string Holes(string s) => string.Join(",", Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().OrderBy(x => x));
        var wrong = Loc.ArabicStrings
            .Where(p => Loc.EnglishStrings.TryGetValue(p.Key, out var en) && Holes(p.Value) != Holes(en))
            .Select(p => p.Key);
        wrong.Should().BeEmpty();
    }

    [Fact]
    public void Every_key_used_in_the_views_exists()
    {
        var app = Path.Combine(RepoRoot(), "src", "HalawaHub.App");
        var keys = Directory.GetFiles(app, "*.axaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{l:Loc (\w+)\}").Select(m => m.Groups[1].Value))
            .Distinct();
        keys.Where(k => !Loc.ArabicStrings.ContainsKey(k)).Should().BeEmpty();
    }

    [Fact]
    public void The_name_is_written_without_a_hyphen()
    {
        Loc.ArabicStrings.Concat(Loc.EnglishStrings).Where(p => p.Value.Contains("Halawa-Hub")).Select(p => p.Key)
            .Should().BeEmpty("the UI name is \"Halawa Hub\"");
        File.ReadAllText(Path.Combine(RepoRoot(), "src", "HalawaHub.App", "ViewModels", "MainViewModel.cs"))
            .Should().Contain("$\"Halawa Hub {AppVersion}\"");
    }

    [Theory]
    [InlineData("favorites", "المفضلة")]
    [InlineData("bpFavs", "المفضلة")]
    [InlineData("openLoc", "افتح مكان الملفات")]
    [InlineData("copyPath", "انسخ المسار")]
    [InlineData("addGame", "أضف لعبة")]
    [InlineData("refresh", "حدّث المكتبة")]
    [InlineData("clear", "امسح البحث")]
    [InlineData("ntDismiss", "أخفِ")]
    [InlineData("bpNtDismiss", "أخفِ")]
    [InlineData("openGh", "افتح GitHub")]
    [InlineData("addFav", "أضف للمفضلة")]
    [InlineData("remFav", "أزل من المفضلة")]
    [InlineData("inFav", "في المفضلة")]
    [InlineData("hide", "أخفِ من المكتبة")]
    [InlineData("startWith", "شغّل مع بدء النظام")]
    [InlineData("closeExit", "أغلق Halawa")]
    [InlineData("bpCloseApp", "أغلق Halawa")]
    [InlineData("bpUpdNow", "حدّث في Steam")]
    [InlineData("bpAutoMain", "افتح الشاشة الكبيرة عند توصيل يد تحكم")]
    [InlineData("bpAutoBp", "افتح الشاشة الكبيرة عند توصيل يد تحكم")]
    [InlineData("verify", "تحقق واحفظ")]
    [InlineData("getKey", "احصل على مفتاح")]
    [InlineData("keyPh", "الصق المفتاح هنا…")]
    public void Arabic_buttons_and_settings_use_the_design_words(string key, string expected) =>
        Loc.Lookup(Loc.Arabic, key).Should().Be(expected);

    [Theory]
    [InlineData("verify", "Check and save")]
    [InlineData("getKey", "Get a key")]
    [InlineData("keyPh", "Paste the key here…")]
    [InlineData("bpAutoMain", "Open Big Picture when a controller connects")]
    [InlineData("bpAutoBp", "Open Big Picture when a controller connects")]
    [InlineData("inFav", "In favorites")]
    public void English_uses_the_design_words(string key, string expected) =>
        Loc.Lookup(Loc.English, key).Should().Be(expected);

    [Fact]
    public void The_SteamGridDB_explanation_is_shown_in_settings_and_the_wizard()
    {
        var views = Path.Combine(RepoRoot(), "src", "HalawaHub.App", "Views");
        File.ReadAllText(Path.Combine(views, "SettingsDialog.axaml")).Should().Contain("{l:Loc sgdbWhy}");
        var wizard = File.ReadAllText(Path.Combine(views, "WizardDialog.axaml"));
        wizard.Should().Contain("{l:Loc sgdbWhy}").And.Contain("{l:Loc verify}");
    }
}
