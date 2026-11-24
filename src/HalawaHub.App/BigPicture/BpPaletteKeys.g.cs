// مولَّد بـ bp6-palette.js من BigPictureWindow.axaml: الألوان والظلال التي تتبع لون التمييز وثيم OLED.
namespace HalawaHub.App.BigPicture;

internal static class BpPaletteKeys
{
    /// AARRGGBB لكل لون يُستعمل كـ BpC_ (فرشاة) وBpK_ (لون)
    internal static readonly string[] Colors =
    {
        "000A0404",
        "14EF4444",
        "1AEF4444",
        "1FEF4444",
        "2EB91C1C",
        "38B91C1C",
        "477F1D1D",
        "59EF4444",
        "80B91C1C",
        "8C0A0404",
        "8CDC2626",
        "99060202",
        "C7060202",
        "C70A0404",
        "E00A0404",
        "F20A0404",
        "FF0A0404",
        "FF0F0607",
        "FF140808",
        "FF1A0A0C",
        "FF231010",
        "FF241010",
        "FF2A1313",
        "FF2E1616",
        "FF3A1518",
        "FF7F1D1D",
        "FF991B1B",
        "FF9C8A8A",
        "FFB8A4A4",
        "FFB91C1C",
        "FFD9C7C7",
        "FFDC2626",
        "FFE8D8D8",
        "FFEF4444",
    };

    /// قوالب الظلال BpS_0 ... بالترتيب
    internal static readonly string[] Shadows =
    {
        "0 0 14 0 #B3EF4444",
        "0 0 18 0 #80EF4444",
        "0 0 16 0 #66EF4444",
        "0 0 14 0 #80EF4444",
        "0 0 26 0 #8CEF4444",
        "0 0 20 0 #73EF4444",
        "0 0 14 0 #73EF4444",
        "0 0 28 0 #99EF4444, inset 0 0 16 0 #40EF4444",
        "0 0 16 0 #A6EF4444",
        "0 0 16 0 #CCEF4444",
        "0 10 30 0 #73EF4444",
        "0 18 44 0 #61EF4444",
        "0 0 22 0 #8CEF4444",
        "0 0 22 0 #99EF4444",
        "0 0 24 0 #8CEF4444",
        "0 0 18 0 #8CEF4444",
    };
}
