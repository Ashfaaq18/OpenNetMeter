using OpenNetMeter.Utilities;

namespace OpenNetMeter.Tests;

public class LatestWinsRunnerTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Request_ReturnsWithoutWaitingForTheWork()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var runner = new LatestWinsRunner(() =>
        {
            entered.Set();
            release.Wait(Generous);
        });

        runner.Request();

        // The caller is back while the work is still blocked.
        Assert.True(entered.Wait(Generous));
        Assert.False(runner.WhenIdle().IsCompleted);

        release.Set();
        await runner.WhenIdle();
    }

    [Fact]
    public async Task ABurstOfRequestsWhileOneRuns_CollapsesIntoOneMoreRun()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var runs = 0;
        var runner = new LatestWinsRunner(() =>
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                entered.Set();
                release.Wait(Generous);
            }
        });

        runner.Request();
        Assert.True(entered.Wait(Generous));
        for (var i = 0; i < 50; i++)
            runner.Request();

        release.Set();
        await runner.WhenIdle();

        // The run in flight, plus exactly one for everything requested meanwhile.
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task TheLastRunSeesTheNewestState()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var state = 0;
        var written = new List<int>();
        var runner = new LatestWinsRunner(() =>
        {
            var snapshot = Volatile.Read(ref state);
            written.Add(snapshot);
            if (written.Count == 1)
            {
                entered.Set();
                release.Wait(Generous);
            }
        });

        Volatile.Write(ref state, 1);
        runner.Request();
        Assert.True(entered.Wait(Generous));

        for (var value = 2; value <= 10; value++)
        {
            Volatile.Write(ref state, value);
            runner.Request();
        }

        release.Set();
        await runner.WhenIdle();

        Assert.Equal(1, written[0]);
        Assert.Equal(10, written[^1]);
        Assert.Equal(2, written.Count);
    }

    [Fact]
    public async Task RunsNeverOverlap_WhateverThreadsRequest()
    {
        var concurrent = 0;
        var maxConcurrent = 0;
        var runs = 0;
        var runner = new LatestWinsRunner(() =>
        {
            var now = Interlocked.Increment(ref concurrent);
            int seen;
            while ((seen = Volatile.Read(ref maxConcurrent)) < now &&
                   Interlocked.CompareExchange(ref maxConcurrent, now, seen) != seen)
            {
            }

            Thread.Sleep(2);
            Interlocked.Increment(ref runs);
            Interlocked.Decrement(ref concurrent);
        });

        var requesters = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < 100; i++)
                    runner.Request();
            }))
            .ToArray();
        await Task.WhenAll(requesters);
        await runner.WhenIdle();

        Assert.Equal(1, maxConcurrent);
        Assert.InRange(runs, 1, 800);
    }

    [Fact]
    public async Task ARequestAfterTheRunnerWentIdle_StartsANewRun()
    {
        var runs = 0;
        var runner = new LatestWinsRunner(() => Interlocked.Increment(ref runs));

        runner.Request();
        await runner.WhenIdle();
        Assert.Equal(1, runs);

        runner.Request();
        await runner.WhenIdle();
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task AFailingRun_DoesNotStopLaterRuns()
    {
        var runs = 0;
        var runner = new LatestWinsRunner(() =>
        {
            if (Interlocked.Increment(ref runs) == 1)
                throw new InvalidOperationException("disk full");
        });

        runner.Request();
        await runner.WhenIdle();
        runner.Request();
        await runner.WhenIdle();

        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task WhenIdle_IsCompleteWhenNothingWasRequested()
    {
        var runner = new LatestWinsRunner(() => { });

        await runner.WhenIdle();

        Assert.True(runner.WhenIdle().IsCompletedSuccessfully);
    }
}
