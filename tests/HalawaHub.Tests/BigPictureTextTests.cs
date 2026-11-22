using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>نصوص الشاشة الكبيرة: كل مفتاح مستعمل موجود باللغتين، والعدد يوافق المعدود.</summary>
public class BigPictureTextTests : IDisposable
{
    public void Dispose() => Loc.Instance.Language = Loc.Arabic;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HalawaHub.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("HalawaHub.sln");
    }

    [Fact]
    public void Every_key_big_picture_uses_exists_in_arabic_and_english()
    {
        var root = RepoRoot();
        var files = Directory.GetFiles(Path.Combine(root, "src", "HalawaHub.App", "BigPicture"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".axaml"))
            .Append(Path.Combine(root, "src", "HalawaHub.App", "ViewModels", "MainViewModel.BigPicture.cs"))
            .Append(Path.Combine(root, "src", "HalawaHub.App", "Views", "TopBar.axaml"))
            .Append(Path.Combine(root, "src", "HalawaHub.App", "Views", "SettingsDialog.axaml"));

        var keys = new HashSet<string>();
        foreach (var text in files.Select(File.ReadAllText))
        {
            foreach (Match m in Regex.Matches(text, "\"(bp[A-Z][A-Za-z0-9]*|bigPicture)\"")) keys.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"\{l:Loc (bp[A-Za-z0-9]*|bigPicture)\}")) keys.Add(m.Groups[1].Value);
        }

        keys.Should().NotBeEmpty();
        keys.Where(k => !Loc.ArabicStrings.ContainsKey(k)).Should().BeEmpty("every key needs an Arabic text");
        keys.Where(k => !Loc.EnglishStrings.ContainsKey(k)).Should().BeEmpty("every key needs an English text");
    }

    [Fact]
    public void Arabic_and_english_have_the_same_big_picture_keys()
    {
        var ar = Loc.ArabicStrings.Keys.Where(k => k.StartsWith("bp") || k == "bigPicture").ToHashSet();
        var en = Loc.EnglishStrings.Keys.Where(k => k.StartsWith("bp") || k == "bigPicture").ToHashSet();
        ar.Except(en).Should().BeEmpty();
        en.Except(ar).Should().BeEmpty();
        Loc.ArabicStrings.Where(p => p.Key.StartsWith("bp")).Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Value));
    }

    [AvaloniaFact]
    public void Arabic_counts_agree_with_their_nouns()
    {
        Loc.Instance.Language = Loc.Arabic;
        BpPlural.HoursPlayed(1).Should().Be("ساعة لعب واحدة");
        BpPlural.HoursPlayed(2).Should().Be("ساعتا لعب");
        BpPlural.HoursPlayed(3).Should().Be("3 ساعات لعب");
        BpPlural.HoursPlayed(142).Should().Be("142 ساعة لعب");
        BpPlural.HoursPlayed(2.5).Should().Be("2.5 ساعة لعب");
        BpPlural.HoursShort(10).Should().Be("10 ساعات");
        BpPlural.HoursShort(11).Should().Be("11 ساعة");
        BpPlural.Games(1).Should().Be("لعبة واحدة");
        BpPlural.Games(2).Should().Be("لعبتان");
        BpPlural.Games(7).Should().Be("7 ألعاب");
        BpPlural.Games(14).Should().Be("14 لعبة");
        BpPlural.DaysAgo(2).Should().Be("قبل يومين");
        BpPlural.DaysAgo(3).Should().Be("قبل 3 أيام");
        BpPlural.MonthsAgo(11).Should().Be("قبل 11 شهرًا");
        BpPlural.HoursAgo(2).Should().Be("قبل ساعتين");
        BpPlural.MinutesAgo(2).Should().Be("قبل دقيقتين");
        BpPlural.MinutesAgo(25).Should().Be("قبل 25 دقيقة");
    }

    [AvaloniaFact]
    public void Arabic_sentences_starting_with_a_game_name_stay_right_to_left()
    {
        Loc.Instance.Language = Loc.Arabic;
        App.BigPicture.Controls.BpAutoText.IsRtlText("Elden Ring تعمل الآن").Should().BeTrue();
        App.BigPicture.Controls.BpAutoText.IsRtlText("Elden Ring").Should().BeFalse();
        App.BigPicture.Controls.BpAutoText.IsRtlText("2077").Should().BeTrue("no letters: follows the interface");
        Loc.Instance.Language = Loc.English;
        App.BigPicture.Controls.BpAutoText.IsRtlText("2077").Should().BeFalse();
    }

    [AvaloniaFact]
    public void English_counts_use_singular_and_plural()
    {
        Loc.Instance.Language = Loc.English;
        BpPlural.Games(1).Should().Be("1 game");
        BpPlural.Games(14).Should().Be("14 games");
        BpPlural.HoursPlayed(142).Should().Be("142 h played");
        BpPlural.DaysAgo(3).Should().Be("3 days ago");
        BpPlural.WeeksAgo(1).Should().Be("Last week");
        BpPlural.MonthsAgo(2).Should().Be("2 months ago");
        BpPlural.HoursAgo(1).Should().Be("1 hour ago");
        BpWindowTitle.Instance.Title.Should().Be("Halawa Hub — Big Picture");
    }
}
