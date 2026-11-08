using FluentAssertions;
using HalawaHub.App;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests;

public class SingleInstanceGuardTests
{
    private static string UniqueId() => "HalawaHubTest-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void SecondInstance_IsNotPrimary_UntilTheFirstIsGone()
    {
        var id = UniqueId();

        var first = SingleInstanceGuard.Acquire(id);
        var second = SingleInstanceGuard.Acquire(id);

        first.IsPrimary.Should().BeTrue();
        second.IsPrimary.Should().BeFalse();

        second.Dispose();
        first.Dispose();

        using var third = SingleInstanceGuard.Acquire(id);
        third.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task SecondInstance_AsksTheFirstToShowItsWindow()
    {
        var id = UniqueId();
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var first = SingleInstanceGuard.Acquire(id);
        first.StartListening(() => activated.TrySetResult());

        using var second = SingleInstanceGuard.Acquire(id);
        second.SignalPrimary(timeoutMs: 5000).Should().BeTrue();

        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Signal_WithNoListener_ReturnsFalse_InsteadOfThrowing()
    {
        using var lonely = SingleInstanceGuard.Acquire(UniqueId());

        lonely.SignalPrimary(timeoutMs: 100).Should().BeFalse();
    }
}

public class TrayCloseBehaviorTests
{
    [Theory]
    [InlineData(false, true, true)]   // إغلاق عادي مع صينية: إخفاء
    [InlineData(false, false, false)] // انحدار: بدون صينية يجب أن يغلق فعليًا
    [InlineData(true, true, false)]   // "خروج" من قائمة الصينية
    [InlineData(true, false, false)]
    public void ShouldHideToTray_OnlyWhenTrayExists(bool allowExit, bool trayAvailable, bool expected)
    {
        MainWindow.ShouldHideToTray(allowExit, trayAvailable).Should().Be(expected);
    }
}

