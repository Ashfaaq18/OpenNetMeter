using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenNetMeter.Utilities;
using Xunit;

namespace OpenNetMeter.Tests;

public class BackgroundWriteQueueTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);

    [Fact]
    public void TryEnqueue_NeverBlocksTheCallerWhileTheSinkIsStuck()
    {
        const int capacity = 4;
        using var sinkEntered = new ManualResetEventSlim();
        using var releaseSink = new ManualResetEventSlim();
        var delivered = new List<int>();

        using var queue = new BackgroundWriteQueue<int>(item =>
        {
            sinkEntered.Set();
            releaseSink.Wait(Generous);
            lock (delivered)
                delivered.Add(item);
        }, capacity, "test-writer");

        // The first item is taken by the writer thread, which then sticks in the sink.
        Assert.True(queue.TryEnqueue(0));
        Assert.True(sinkEntered.Wait(Generous));

        var clock = Stopwatch.StartNew();
        int accepted = 0;
        for (int i = 1; i <= 100; i++)
        {
            if (queue.TryEnqueue(i))
                accepted++;
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), "TryEnqueue blocked while the sink was stuck");
        Assert.Equal(capacity, accepted);
        Assert.Equal(100 - capacity, queue.DroppedCount);

        releaseSink.Set();
        Assert.True(queue.Flush(Generous));

        // The in-flight item plus what fit in the queue; the rest were dropped, in order.
        lock (delivered)
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, delivered);
    }

    [Fact]
    public void Flush_WaitsUntilEverythingQueuedBeforeItWasDelivered()
    {
        var delivered = new List<int>();

        using var queue = new BackgroundWriteQueue<int>(item =>
        {
            Thread.Sleep(5);
            lock (delivered)
                delivered.Add(item);
        }, 100, "test-writer");

        for (int i = 0; i < 10; i++)
            Assert.True(queue.TryEnqueue(i));

        Assert.True(queue.Flush(Generous));

        lock (delivered)
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, delivered);
    }

    [Fact]
    public void Flush_TimesOutWhileTheSinkIsStuck()
    {
        using var sinkEntered = new ManualResetEventSlim();
        using var releaseSink = new ManualResetEventSlim();

        using var queue = new BackgroundWriteQueue<int>(_ =>
        {
            sinkEntered.Set();
            releaseSink.Wait(Generous);
        }, 4, "test-writer");

        queue.TryEnqueue(0);
        Assert.True(sinkEntered.Wait(Generous));

        Assert.False(queue.Flush(TimeSpan.FromMilliseconds(100)));

        releaseSink.Set();
    }

    [Fact]
    public void ASinkFailureDoesNotStopLaterItems()
    {
        var delivered = new List<int>();

        using var queue = new BackgroundWriteQueue<int>(item =>
        {
            if (item == 0)
                throw new InvalidOperationException("sink failed");

            lock (delivered)
                delivered.Add(item);
        }, 10, "test-writer");

        queue.TryEnqueue(0);
        queue.TryEnqueue(1);

        Assert.True(queue.Flush(Generous));

        lock (delivered)
            Assert.Equal(new[] { 1 }, delivered);
    }
}
