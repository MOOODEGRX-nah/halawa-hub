using System.Diagnostics;
using System.Globalization;
using FluentAssertions;
using HalawaHub.App.Converters;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.Core;
using HalawaHub.Core.Models;
using Xunit;

namespace HalawaHub.Tests;

public class LinuxDesktopEntryTests
{
    [Theory]
    [InlineData("/home/u/app/Halawa-Hub", "\"/home/u/app/Halawa-Hub\"")]
    [InlineData("/home/u/My Games/Halawa-Hub", "\"/home/u/My Games/Halawa-Hub\"")]
    [InlineData("/home/u/100%/Hub", "\"/home/u/100%%/Hub\"")]
    [InlineData("/home/u/a$b/Hub", "\"/home/u/a\\\\$b/Hub\"")]
    [InlineData("/home/u/a\"b/Hub", "\"/home/u/a\\\\\"b/Hub\"")]
    [InlineData("/home/u/a`b/Hub", "\"/home/u/a\\\\`b/Hub\"")]
    [InlineData("/home/u/a\\b/Hub", "\"/home/u/a\\\\\\\\b/Hub\"")]
    public void Exec_IsQuotedPerTheDesktopEntrySpec(string path, string expected)
    {
        LinuxDesktopEntry.QuoteExec(path).Should().Be(expected);
    }

    [Fact]
    public void Entry_HasEverythingTheMenuAndTaskbarNeed()
    {
        var entry = LinuxDesktopEntry.Build("/opt/hub/Halawa-Hub");

        entry.Should().StartWith("[Desktop Entry]\n");
        entry.Should().Contain("Type=Application\n");
        entry.Should().Contain("Name=Halawa-Hub\n");
        entry.Should().Contain("Exec=\"/opt/hub/Halawa-Hub\"\n");
        entry.Should().Contain("Icon=halawa-hub\n");
        entry.Should().Contain("Terminal=false\n");
        entry.Should().Contain("StartupWMClass=Halawa-Hub\n");
        entry.Should().NotContain("\r", "desktop files use LF on every system");
    }

    [Fact]
    public void XdgFolders_FollowTheEnvironment_WithTheStandardDefaults()
    {
        LinuxDesktopEntry.ConfigHome(null, "/home/u").Should().Be(Path.Combine("/home/u", ".config"));
        LinuxDesktopEntry.ConfigHome("  ", "/home/u").Should().Be(Path.Combine("/home/u", ".config"));
        LinuxDesktopEntry.ConfigHome("/custom", "/home/u").Should().Be("/custom");
        LinuxDesktopEntry.DataHome(null, "/home/u").Should().Be(Path.Combine("/home/u", ".local", "share"));
        LinuxDesktopEntry.DataHome("/custom", "/home/u").Should().Be("/custom");
    }
}

public sealed class LinuxAutostartTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Autostart-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public LinuxAutostartTests()
    {
        _path = Path.Combine(_root, "autostart", "halawa-hub.desktop");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Path_IsTheXdgAutostartLocation()
    {
        StartupService.AutostartPath(null, "/home/u")
            .Should().Be(Path.Combine("/home/u", ".config", "autostart", "halawa-hub.desktop"));
        StartupService.AutostartPath("/custom", "/home/u")
            .Should().Be(Path.Combine("/custom", "autostart", "halawa-hub.desktop"));
    }

    [Fact]
    public void Enable_WritesTheEntry_Disable_RemovesIt()
    {
        StartupService.IsAutostartEnabled(_path).Should().BeFalse();

        StartupService.SetAutostart(_path, true, "/opt/My Hub/Halawa-Hub");

        StartupService.IsAutostartEnabled(_path).Should().BeTrue();
        File.ReadAllText(_path).Should().Contain("Exec=\"/opt/My Hub/Halawa-Hub\"\n");
        Directory.GetFiles(Path.GetDirectoryName(_path)!).Should().ContainSingle("no temporary file stays behind");

        StartupService.SetAutostart(_path, false, "/opt/My Hub/Halawa-Hub");

        StartupService.IsAutostartEnabled(_path).Should().BeFalse();
        File.Exists(_path).Should().BeFalse();
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_DoesNotThrow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        StartupService.SetAutostart(_path, false, "/x");

        File.Exists(_path).Should().BeFalse();
    }

    [Fact]
    public void Enable_WithoutAKnownPath_WritesNothing()
    {
        StartupService.SetAutostart(_path, true, null);

        File.Exists(_path).Should().BeFalse("an entry with an empty Exec would fail silently at login");
    }

    [Fact]
    public void HiddenEntry_CountsAsDisabled()
    {
        // هكذا تعطّل إعدادات KDE وGNOME التشغيل التلقائي بدون حذف الملف
        StartupService.SetAutostart(_path, true, "/x");
        File.AppendAllText(_path, "Hidden=true\n");

        StartupService.IsAutostartEnabled(_path).Should().BeFalse();
    }

    [Fact]
    public void MovedAppImage_FixesThePathOnNextStart()
    {
        StartupService.SetAutostart(_path, true, "/old/Hub.AppImage");

        StartupService.RefreshAutostart(_path, "/new/Hub.AppImage");

        File.ReadAllText(_path).Should().Contain("Exec=\"/new/Hub.AppImage\"\n");
    }

    [Fact]
    public void Refresh_DoesNotEnableADisabledEntry()
    {
        StartupService.RefreshAutostart(_path, "/new/Hub.AppImage");

        File.Exists(_path).Should().BeFalse();
    }

    [Fact]
    public void Tests_NeverTouchTheRealAutostartFolder()
    {
        if (!OperatingSystem.IsLinux()) return;

        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME").Should().StartWith(Path.GetTempPath());
        Environment.GetEnvironmentVariable("XDG_DATA_HOME").Should().StartWith(Path.GetTempPath());
    }
}

public class AppMenuEntryTests
{
    [Fact]
    public void Entry_PointsAtTheAppImage_AndIsMarkedAsOurs()
    {
        var entry = AppMenuService.BuildEntry("/apps/Hub.AppImage");

        entry.Should().Contain("Exec=\"/apps/Hub.AppImage\"\n");
        entry.Should().EndWith("X-Halawa-Hub-AppImage=true\n");
    }

    [Fact]
    public void EntryFromTheTarballInstaller_IsNotOurs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"menu-{Guid.NewGuid():N}.desktop");
        try
        {
            File.WriteAllText(path, LinuxDesktopEntry.Build("/home/u/.local/opt/halawa-hub/Halawa-Hub"));
            AppMenuService.IsOwnEntry(path).Should().BeFalse("the toggle must not delete what install.sh wrote");

            File.WriteAllText(path, AppMenuService.BuildEntry("/apps/Hub.AppImage"));
            AppMenuService.IsOwnEntry(path).Should().BeTrue();
        }
        finally
        {
            File.Delete(path);
        }

        AppMenuService.IsOwnEntry(path).Should().BeFalse();
    }
}

public class LinuxSingleInstanceTests
{
    [Fact]
    public void LockFile_LivesInTheUserRuntimeFolder_OrFallsBackToData()
    {
        var runtime = Path.GetTempPath();

        SingleInstanceGuard.LockFilePath("Id", "AB", runtime).Should().Be(Path.Combine(runtime, "Id-SingleInstance-AB.lock"));
        SingleInstanceGuard.LockFilePath("Id", "AB", null).Should().Be(AppPaths.InData("Id-SingleInstance-AB.lock"));
        SingleInstanceGuard.LockFilePath("Id", "AB", "/nonexistent/run").Should().Be(AppPaths.InData("Id-SingleInstance-AB.lock"));
    }

    /// <summary>
    /// انحدار: الـ Mutex المسمى على لينكس محصور في جلسة العملية، فالتشغيل من قائمة
    /// التطبيقات ثم من الطرفية كان يفتح نسختين. عملية خارجية في جلسة جديدة (setsid)
    /// يجب أن ترى قفل النسخة الأولى، وألا تراه بعد تحريره.
    /// </summary>
    [Fact]
    public void Lock_IsSeenFromAnotherSession_AndIsReleasedOnDispose()
    {
        if (!OperatingSystem.IsLinux() || !ToolExists("setsid") || !ToolExists("flock")) return;

        var id = "HalawaHubTest-" + Guid.NewGuid().ToString("N");
        var lockFile = FindLockFile(id, () =>
        {
            using var guard = SingleInstanceGuard.Acquire(id);
            guard.IsPrimary.Should().BeTrue();
        });

        using (var guard = SingleInstanceGuard.Acquire(id))
        {
            guard.IsPrimary.Should().BeTrue();
            TryLockFromNewSession(lockFile).Should().BeFalse("another session must see the running instance");
        }

        TryLockFromNewSession(lockFile).Should().BeTrue("the lock is gone once the instance is");

        File.Delete(lockFile);
    }

    private static string FindLockFile(string id, Action create)
    {
        create();
        var folder = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var searchIn = !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) ? folder : AppPaths.DataDirectory;
        return Directory.GetFiles(searchIn, id + "-SingleInstance-*.lock").Single();
    }

    private static bool TryLockFromNewSession(string lockFile)
    {
        var psi = new ProcessStartInfo("setsid") { UseShellExecute = false, RedirectStandardError = true };
        foreach (var arg in new[] { "--wait", "flock", "--nonblock", lockFile, "true" }) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        process.WaitForExit(10_000).Should().BeTrue();
        return process.ExitCode == 0;
    }

    private static bool ToolExists(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':')
            .Any(dir => File.Exists(Path.Combine(dir, tool)));
}

public class LinuxPlatformUiTests
{
    private static GameCardViewModel Card(string platform, string executable) =>
        new(new GameInfo { Id = "1", Name = "G", Platform = platform, ExecutablePath = executable }, Array.Empty<HalawaHub.Core.Plugins.IGameTool>());

    [Theory]
    [InlineData("steam://rungameid/1", true)]
    [InlineData("heroic://launch/legendary/Sugar", true)]
    [InlineData("lutris:rungameid/5", true)]
    [InlineData("/opt/game/run.sh", false)]
    [InlineData("", false)]
    public void UrlLaunches_AreRecognised(string executable, bool expected)
    {
        GameCardViewModel.IsUrlLaunch(executable).Should().Be(expected);
    }

    [Theory]
    [InlineData("Steam", true)]
    [InlineData("Heroic", false)]
    [InlineData("Lutris", false)]
    public void Uninstall_IsOnlyOfferedWhereItWorks(string platform, bool expected)
    {
        if (!OperatingSystem.IsLinux()) return;

        Card(platform, "x://y").CanUninstall.Should().Be(expected);
    }

    [Fact]
    public void Uninstall_StaysVisibleOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        Card("Epic Games", "x://y").CanUninstall.Should().BeTrue("Windows keeps its explanatory message");
    }

    [Fact]
    public void LaunchParameters_AreDisabledForUrlLaunches_OnLinuxOnly()
    {
        var card = Card("Heroic", "heroic://launch/legendary/Sugar");

        card.SupportsLaunchParameters.Should().Be(!OperatingSystem.IsLinux());
        Card("Plugin", "/opt/game/run.sh").SupportsLaunchParameters.Should().BeTrue();
    }

    [Theory]
    [InlineData("Heroic")]
    [InlineData("Lutris")]
    public void NewLaunchers_HaveTheirOwnColor(string platform)
    {
        var fallback = new PlatformColorConverter().Convert("Unknown Plugin", typeof(object), null, CultureInfo.InvariantCulture).ToString();

        new PlatformColorConverter().Convert(platform, typeof(object), null, CultureInfo.InvariantCulture).ToString()
            .Should().NotBe(fallback);
    }

    [Theory]
    [InlineData("appMenu")]
    [InlineData("appMenuNote")]
    [InlineData("launchUrlNote")]
    [InlineData("startNote")]
    [InlineData("updUnsupported")]
    public void NewStrings_ExistInBothLanguages(string key)
    {
        var loc = Loc.Instance;
        var previous = loc.Language;
        try
        {
            loc.Language = Loc.Arabic;
            var arabic = loc[key];
            loc.Language = Loc.English;
            var english = loc[key];

            arabic.Should().NotBe(key);
            english.Should().NotBe(key);
            arabic.Should().NotBe(english);
        }
        finally
        {
            loc.Language = previous;
        }
    }

    [Fact]
    public void Texts_NoLongerClaimWindowsOnly()
    {
        var loc = Loc.Instance;
        var previous = loc.Language;
        try
        {
            loc.Language = Loc.English;
            loc["startNote"].Should().NotContain("Windows");
            loc["updUnsupported"].Should().NotContain("Windows");
        }
        finally
        {
            loc.Language = previous;
        }
    }
}
