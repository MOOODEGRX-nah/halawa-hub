using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.Core.Library;
using Xunit;

namespace HalawaHub.Tests;

/// الثيم الفاتح: كل نص على خلفيته يحقق تباين WCAG AA (4.5:1)، وكل ألوان التمييز تحمل نصًا أبيض
public class ThemeContrastTests
{
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("oled")]
    public void TextOnBackgrounds_PassesAA(string theme)
    {
        var p = ThemeManager.PaletteFor(theme);
        foreach (var bg in new[] { "PageBrush", "SurfaceBrush", "PanelBrush", "DialogBrush", "MenuBrush" })
        foreach (var fg in new[] { "TextBrush", "MutedBrush", "ChipTextBrush", "SubtleBrush", "MenuTextBrush", "AmberBrush",
                     "StatusUpdateBrush", "StatusRepairBrush", "StatusRunningBrush" })
        {
            ThemeManager.Contrast(p[fg], p[bg]).Should().BeGreaterThanOrEqualTo(4.5, $"{fg} on {bg} in {theme}");
        }
    }

    [Theory]
    [InlineData("red")]
    [InlineData("blue")]
    [InlineData("purple")]
    [InlineData("green")]
    [InlineData("orange")]
    public void Accents_CarryWhiteText_AndTitlesAreReadableOnLight(string accent)
    {
        var a = ThemeManager.AccentFor(accent);
        ThemeManager.Contrast("#FFFFFF", a.Base).Should().BeGreaterThanOrEqualTo(4.5, "white button text");
        ThemeManager.Contrast("#FFFFFF", a.Deep).Should().BeGreaterThanOrEqualTo(4.5, "white text on chosen options");
        ThemeManager.Contrast(ThemeManager.AccentTextColor("light", accent), ThemeManager.PaletteFor("light")["DialogBrush"])
            .Should().BeGreaterThanOrEqualTo(4.5, "accent titles on the light theme");
    }

    [Fact]
    public void UnknownValues_FallBackToTheOriginalLook()
    {
        ThemeManager.Normalize("pink").Should().Be("dark");
        ThemeManager.NormalizeAccent(null).Should().Be("red");
        // الثيم الداكن الأصلي بقيمه حرفيًا
        ThemeManager.PaletteFor("dark")["PageBrush"].Should().Be("#0A0404");
        ThemeManager.AccentFor("red").Base.Should().Be("#DC2626");
    }
}

public class MotionPreferenceTests
{
    [Theory]
    [InlineData("[General]\nfoo=1\n[KDE]\nAnimationDurationFactor=0\nSingleClick=false\n", true)]
    [InlineData("[KDE]\nAnimationDurationFactor=0.5\n", false)]
    [InlineData("[KDE]\nSingleClick=false\n", null)]
    [InlineData("[Other]\nAnimationDurationFactor=0\n", null)]
    [InlineData("", null)]
    public void KdeAnimationFactor_IsRead(string kdeglobals, bool? reduced)
    {
        MotionPreferences.KdePrefersReduced(kdeglobals).Should().Be(reduced);
    }

    [Fact]
    public void OnAndOff_SetMotion()
    {
        var before = MotionSettings.Enabled;
        try
        {
            MotionPreferences.Apply("on");
            MotionSettings.Enabled.Should().BeFalse();
            MotionPreferences.Apply("off");
            MotionSettings.Enabled.Should().BeTrue();
            MotionPreferences.Apply("garbage");
            MotionPreferences.Current.Should().Be("off");
        }
        finally
        {
            MotionSettings.Enabled = before;
        }
    }
}

public class DataWipeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "halawahub-wipe-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void NothingIsDeletedWithoutTheMarker()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "playtime.json"), "{}");
        DataWipe.RunIfScheduled(_dir).Should().BeFalse();
        File.Exists(Path.Combine(_dir, "playtime.json")).Should().BeTrue();
    }

    [Fact]
    public void WithTheMarker_EverythingInTheDataFolderGoes()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "cache", "covers"));
        File.WriteAllText(Path.Combine(_dir, "playtime.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "cache", "covers", "a.png"), "x");
        File.WriteAllText(Path.Combine(_dir, DataWipe.MarkerName), "now");

        DataWipe.RunIfScheduled(_dir).Should().BeTrue();
        Directory.EnumerateFileSystemEntries(_dir).Should().BeEmpty();
        Directory.Exists(_dir).Should().BeTrue("only the contents are deleted");
    }
}

public class PlatformCatalogTests
{
    [Fact]
    public void Capabilities_AreHonest()
    {
        PlatformCatalog.For("Steam").Updates.Should().BeTrue();
        PlatformCatalog.For("Epic Games").Updates.Should().BeFalse();
        PlatformCatalog.For("Epic Games").RepairOnly.Should().BeTrue();
        PlatformCatalog.For("GOG").RunningOutside.Should().BeFalse();
        PlatformCatalog.For("SomePlugin").PlayTime.Should().BeFalse();
        PlatformCatalog.For(ManualGames.Platform).PlayTime.Should().BeTrue();
    }
}
