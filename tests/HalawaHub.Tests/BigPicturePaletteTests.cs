using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using FluentAssertions;
using HalawaHub.App.BigPicture;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>تحديث التصميم 6.4: الشاشة الكبيرة تتبع لون التمييز وثيم OLED، وتبقى داكنة مع الثيم الفاتح.</summary>
public class BigPicturePaletteTests : IDisposable
{
    public void Dispose() => BpPalette.Use("red", "dark");

    [AvaloniaFact]
    public void Red_becomes_the_chosen_accent_and_keeps_its_transparency()
    {
        BpPalette.Use("blue", "dark");
        BpPalette.Map("#EF4444").Should().Be(Color.Parse("#3B82F6"));
        BpPalette.Map("#8CEF4444").Should().Be(Color.Parse("#8C3B82F6"));
        BpPalette.Map("#7F1D1D").Should().Be(Color.Parse("#1E3A8A"));
        BpPalette.Map("#0A0404").Should().Be(Color.Parse("#0A0404"), "the dark theme keeps its warm neutrals");
        BpPalette.Map("#F5A623").Should().Be(Color.Parse("#F5A623"), "amber is not an accent colour");
    }

    [AvaloniaFact]
    public void Oled_turns_the_neutrals_black_and_light_stays_dark()
    {
        BpPalette.Use("green", "oled");
        BpPalette.Map("#0A0404").Should().Be(Color.Parse("#000000"));
        BpPalette.Map("#140808").Should().Be(Color.Parse("#0A0A0A"));
        BpPalette.Map("#DC2626").Should().Be(Color.Parse("#15803D"));

        BpPalette.Use("red", "light");
        BpPalette.Oled.Should().BeFalse();
        BpPalette.Map("#0A0404").Should().Be(Color.Parse("#0A0404"), "Big Picture stays dark for the TV");
    }

    [AvaloniaFact]
    public void Window_resources_and_shadows_are_recoloured()
    {
        BpPalette.Use("purple", "oled");
        var res = new ResourceDictionary();
        BpPalette.Apply(res);
        ((ISolidColorBrush)res["BpC_FFEF4444"]!).Color.Should().Be(Color.Parse("#A855F7"));
        ((ISolidColorBrush)res["BpSurface"]!).Color.Should().Be(Color.Parse("#0A0A0A"));
        BpPalette.Recolor("0 0 26 0 #8CEF4444").Should().Be("0 0 26 0 #8CA855F7");
        for (var i = 0; i < BpPaletteKeys.Shadows.Length; i++) res.ContainsKey("BpS_" + i).Should().BeTrue();
    }
}
