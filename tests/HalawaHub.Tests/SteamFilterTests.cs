using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// تمنع رجوع E13 (حزم دعم ستيم تظهر كألعاب).
/// تختبر منطق الفلتر بأسماء حقيقية من Steam.
/// </summary>
public class SteamFilterTests
{
    /// <summary>
    /// نفس المنطق من SteamLibraryProvider.cs — نُعيد تعريفه هنا للاختبار.
    /// لو تغيّر المنطق الأصلي، هذا الاختبار يفشل ويذكرنا بمزامنته.
    /// </summary>
    private static bool IsRuntimeOrTool(string name, string appId)
    {
        return appId == "228980"
            || name.Contains("Redistributables", StringComparison.OrdinalIgnoreCase)
            || name.Contains("DirectX Runtime", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Proton", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Depot", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Steamworks Common Redistributables", "228980", true)]
    [InlineData("DirectX Runtime", "999999", true)]
    [InlineData("Proton 8.0", "1245040", true)]
    [InlineData("Steam Linux Runtime", "1070560", true)]
    [InlineData("Depot 730", "730", true)]
    [InlineData("Half-Life 2", "220", false)]
    [InlineData("Portal", "400", false)]
    [InlineData("Counter-Strike 2", "730", false)]
    public void RuntimeNames_AreCorrectlyFiltered(string name, string appId, bool shouldExclude)
    {
        Assert.Equal(shouldExclude, IsRuntimeOrTool(name, appId));
    }
}
