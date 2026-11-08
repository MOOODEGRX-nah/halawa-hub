using FluentAssertions;
using HalawaHub.App.Services;
using Xunit;

namespace HalawaHub.Tests;

public class SingleFlightRunnerTests
{
    [Fact]
    public async Task Run_ReturnsBeforeWorkFinishes_AndUsesAnotherThread()
    {
        var runner = new SingleFlightRunner();
        using var release = new ManualResetEventSlim(false);
        var workThread = -1;

        var task = runner.RunAsync(() =>
        {
            workThread = Environment.CurrentManagedThreadId;
            release.Wait(TimeSpan.FromSeconds(10));
            return 7;
        });

        task.IsCompleted.Should().BeFalse("the caller (UI thread) must not wait for the scan");
        runner.IsRunning.Should().BeTrue();

        release.Set();
        var (ran, result) = await task;

        ran.Should().BeTrue();
        result.Should().Be(7);
        workThread.Should().NotBe(-1);
        runner.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task Run_WhileRunning_IsIgnored()
    {
        var runner = new SingleFlightRunner();
        using var release = new ManualResetEventSlim(false);
        var calls = 0;

        var first = runner.RunAsync(() => { calls++; release.Wait(TimeSpan.FromSeconds(10)); return 1; });
        var second = await runner.RunAsync(() => { calls++; return 2; });

        second.Ran.Should().BeFalse();

        release.Set();
        await first;
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Run_AfterFailure_CanRunAgain()
    {
        var runner = new SingleFlightRunner();

        var act = () => runner.RunAsync<int>(() => throw new InvalidOperationException("boom"));
        await act.Should().ThrowAsync<InvalidOperationException>();

        runner.IsRunning.Should().BeFalse();
        (await runner.RunAsync(() => 1)).Ran.Should().BeTrue();
    }
}

public class DebouncerTests
{
    [Fact]
    public async Task Schedule_RunsOnce_AfterTheLastCall()
    {
        var runs = 0;
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(40), () => Interlocked.Increment(ref runs));

        debouncer.Schedule();
        debouncer.Schedule();
        debouncer.Schedule();
        Volatile.Read(ref runs).Should().Be(0);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Volatile.Read(ref runs) == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
        await Task.Delay(120);

        Volatile.Read(ref runs).Should().Be(1);
    }

    [Fact]
    public async Task Flush_RunsImmediately_AndCancelsThePendingRun()
    {
        var runs = 0;
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(30), () => Interlocked.Increment(ref runs));

        debouncer.Schedule();
        debouncer.Flush();
        Volatile.Read(ref runs).Should().Be(1);

        await Task.Delay(100);
        Volatile.Read(ref runs).Should().Be(1);
    }

    [Fact]
    public void Flush_WithNothingPending_DoesNothing()
    {
        var runs = 0;
        new Debouncer(TimeSpan.FromSeconds(1), () => runs++).Flush();
        runs.Should().Be(0);
    }
}

