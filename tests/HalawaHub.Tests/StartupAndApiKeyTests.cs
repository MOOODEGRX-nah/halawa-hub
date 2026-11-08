using FluentAssertions;
using HalawaHub.App.Services;
using HalawaHub.App.ViewModels;
using HalawaHub.Core.Library;
using HalawaHub.Core.Models;
using HalawaHub.Core.Plugins;
using Moq;
using Xunit;

namespace HalawaHub.Tests;

/// <summary>
/// انحدار: تجمّد الواجهة عند الفتح، وزر التحقق المعطل، والمفتاح الذي لا يعمل قبل
/// إعادة التشغيل، والحفظ عند كل حرف. مجلد البيانات مؤقت (TestDataIsolation).
/// </summary>
public class StartupAndApiKeyTests
{
    private static Mock<IUpdateService> UpToDateService()
    {
        var update = new Mock<IUpdateService>();
        update.Setup(u => u.CheckForUpdateAsync())
            .ReturnsAsync(new UpdateCheckResult(false, null, null, null, "ok"));
        return update;
    }

    private static MainViewModel NewViewModel(ILibraryService library) =>
        new(library, new FilterService(), UpToDateService().Object, startBackgroundTasks: false);

    private static Mock<ILibraryService> LibraryReturning(params GameInfo[] games)
    {
        var library = new Mock<ILibraryService>();
        library.Setup(l => l.ScanAllLibraries(It.IsAny<IEnumerable<IGameLibraryProvider>>(), It.IsAny<IEnumerable<IGameTool>>()))
            .Returns(() =>
            {
                var result = new LibraryScanResult();
                result.Games.AddRange(games);
                return result;
            });
        return library;
    }

    [Fact]
    public async Task Constructor_DoesNotWaitForTheLibraryScan()
    {
        using var release = new ManualResetEventSlim(false);
        using var started = new ManualResetEventSlim(false);
        var game = new GameInfo { Id = "1", Name = "G", Platform = "Steam" };

        var library = new Mock<ILibraryService>();
        library.Setup(l => l.ScanAllLibraries(It.IsAny<IEnumerable<IGameLibraryProvider>>(), It.IsAny<IEnumerable<IGameTool>>()))
            .Returns(() =>
            {
                started.Set();
                release.Wait(TimeSpan.FromSeconds(30));
                var result = new LibraryScanResult();
                result.Games.Add(game);
                return result;
            });

        // لو عاد الفحص ليعمل على خيط الواجهة، هذا السطر يتجمد 30 ثانية
        var buildTask = Task.Run(() => NewViewModel(library.Object));
        var vm = await buildTask.WaitAsync(TimeSpan.FromSeconds(10));

        started.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        vm.IsRefreshing.Should().BeTrue("the spinner must show while the scan runs");
        vm.Games.Should().BeEmpty("results arrive after the window opens");

        release.Set();
        await WaitUntilAsync(() => !vm.IsRefreshing);

        vm.Games.Should().ContainSingle();
    }

    [Fact]
    public async Task Refresh_WhileScanning_IsIgnored()
    {
        using var release = new ManualResetEventSlim(false);
        var calls = 0;

        var library = new Mock<ILibraryService>();
        library.Setup(l => l.ScanAllLibraries(It.IsAny<IEnumerable<IGameLibraryProvider>>(), It.IsAny<IEnumerable<IGameTool>>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref calls);
                release.Wait(TimeSpan.FromSeconds(30));
                return new LibraryScanResult();
            });

        var vm = NewViewModel(library.Object);
        await WaitUntilAsync(() => Volatile.Read(ref calls) == 1);

        vm.RefreshCommand.Execute(null);
        vm.RefreshCommand.Execute(null);

        release.Set();
        await WaitUntilAsync(() => !vm.IsRefreshing);

        calls.Should().Be(1);
    }

    [Fact]
    public async Task ApiKey_EnablesVerifyButton_AndRaisesCanExecuteChanged()
    {
        var vm = NewViewModel(LibraryReturning().Object);
        await WaitUntilAsync(() => !vm.IsRefreshing);

        vm.SteamGridDbApiKey = "";
        vm.VerifyApiKeyCommand.CanExecute(null).Should().BeFalse();

        var raised = 0;
        vm.VerifyApiKeyCommand.CanExecuteChanged += (_, _) => raised++;

        vm.SteamGridDbApiKey = "some-key";

        raised.Should().BeGreaterThan(0, "the button otherwise stays disabled forever");
        vm.VerifyApiKeyCommand.CanExecute(null).Should().BeTrue();

        vm.SteamGridDbApiKey = "";
        vm.FlushPendingSettings();
    }

    [Fact]
    public async Task ApiKey_IsNotSavedOnEveryKeystroke_ButFlushSavesIt()
    {
        var vm = NewViewModel(LibraryReturning().Object);
        await WaitUntilAsync(() => !vm.IsRefreshing);

        vm.SteamGridDbApiKey = "";
        vm.FlushPendingSettings();

        vm.SteamGridDbApiKey = "a";
        vm.SteamGridDbApiKey = "ab";
        vm.SteamGridDbApiKey = "abc";

        ConfigService.Load().SteamGridDbApiKey.Should().Be("", "nothing is written while typing");

        vm.FlushPendingSettings();

        ConfigService.Load().SteamGridDbApiKey.Should().Be("abc");

        vm.SteamGridDbApiKey = "";
        vm.FlushPendingSettings();
    }

    [Fact]
    public async Task NewApiKey_IsUsedWithoutRestart()
    {
        var vm = NewViewModel(LibraryReturning().Object);
        await WaitUntilAsync(() => !vm.IsRefreshing);

        vm.SteamGridDbApiKey = "";
        vm.FlushPendingSettings();
        vm.IsCoverClientConfigured.Should().BeFalse();

        vm.SteamGridDbApiKey = "new-key";
        vm.FlushPendingSettings();

        vm.IsCoverClientConfigured.Should().BeTrue();

        vm.SteamGridDbApiKey = "";
        vm.FlushPendingSettings();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("condition not met");
            await Task.Delay(10);
        }
    }
}
