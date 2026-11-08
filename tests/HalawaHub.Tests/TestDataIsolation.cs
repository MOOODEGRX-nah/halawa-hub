using System.Runtime.CompilerServices;
using FluentAssertions;
using HalawaHub.Core;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// يحوّل مجلد البيانات لمجلد مؤقت قبل تحميل أي خدمة، حتى لا يكتب أي اختبار
/// في favorites.json أو config.json الحقيقيين على جهاز المطور.
/// </summary>
internal static class TestDataIsolation
{
    [ModuleInitializer]
    internal static void RedirectUserData()
    {
        var dir = Path.Combine(Path.GetTempPath(), "HalawaHub-Tests-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(AppPaths.DataDirOverrideVariable, dir);
    }
}

public class AppPathsTests
{
    [Fact]
    public void Tests_NeverUseTheRealUserDataFolder()
    {
        var real = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HalawaHub");

        AppPaths.DataDirectory.Should().NotBe(real);
        AppPaths.DataDirectory.Should().StartWith(Path.GetTempPath());
    }

    [Fact]
    public void ResolveDataDirectory_WithoutOverride_KeepsTheLegacyPath()
    {
        // بيانات المستخدمين الحاليين يجب أن تبقى في مكانها بالضبط
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HalawaHub");

        AppPaths.ResolveDataDirectory(null).Should().Be(expected);
        AppPaths.ResolveDataDirectory("  ").Should().Be(expected);
    }

    [Fact]
    public void ResolveDataDirectory_WithOverride_UsesIt()
    {
        AppPaths.ResolveDataDirectory("/some/dir").Should().Be("/some/dir");
    }
}
