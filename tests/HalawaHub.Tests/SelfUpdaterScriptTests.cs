using FluentAssertions;
using HalawaHub.Core.Updates;
using Xunit;

namespace HalawaHub.Tests;

public class SelfUpdaterScriptTests
{
    [Fact]
    public void Script_HasNoUnresolvedLogPathVariable_AndLogsEverythingToTheRealPath()
    {
        const string log = @"C:\Temp\HH\update.log";

        var script = SelfUpdater.BuildApplyScript(log, "Halawa-Hub.exe", @"C:\Temp\HH\extracted", @"C:\Apps\HH");

        script.Should().NotContain("%logPath%", "it is a C# variable, not a batch one");
        script.Should().Contain($"echo Update completed >> \"{log}\"");
        script.Should().Contain("del \"%~f0\"");
    }

    [Fact]
    public async Task DownloadAndApply_OutsideWindows_RefusesWithoutTouchingTheNetwork()
    {
        if (OperatingSystem.IsWindows()) return; // الحارس لا يعمل على ويندوز، وهذا الاختبار لا يجوز أن يُنزّل شيئًا

        string? status = null;
        var ok = await SelfUpdater.DownloadAndApplyAsync("http://127.0.0.1:1/never", s => status = s);

        ok.Should().BeFalse();
        status.Should().NotBeNull();
    }
}
