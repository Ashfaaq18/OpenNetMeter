using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace OpenNetMeter.Utilities;

/// <summary>
/// Hands items to one background thread so a caller never waits on a slow sink (the Windows Event Log).
/// The queue is bounded: when the sink falls behind, new items are dropped and counted instead of blocking.
/// </summary>
internal sealed class BackgroundWriteQueue<T> : IDisposable
{
    private readonly struct Item
    {
        public Item(T? value, ManualResetEventSlim? flushSignal)
        {
            Value = value;
            FlushSignal = flushSignal;
        }

        public T? Value { get; }
        public ManualResetEventSlim? FlushSignal { get; }
    }

    private readonly BlockingCollection<Item> queue;
    private readonly Action<T> sink;
    private long droppedCount;

    public BackgroundWriteQueue(Action<T> sink, int capacity, string threadName)
    {
        this.sink = sink;
        queue = new BlockingCollection<Item>(capacity);

        // Background, so a stuck sink can never keep the process alive; Flush covers a clean exit.
        new Thread(Run) { IsBackground = true, Name = threadName }.Start();
    }

    /// <summary>Entries discarded because the queue was full.</summary>
    public long DroppedCount => Interlocked.Read(ref droppedCount);

    /// <summary>Queues the item without waiting. Returns false (and counts a drop) when the queue is full.</summary>
    public bool TryEnqueue(T value)
    {
        try
        {
            if (queue.TryAdd(new Item(value, null)))
                return true;
        }
        catch (InvalidOperationException)
        {
            // Disposed.
        }

        Interlocked.Increment(ref droppedCount);
        return false;
    }

    /// <summary>
    /// Waits until everything queued before this call has been handed to the sink, or the timeout elapses.
    /// Meant for process exit, the one place where blocking is acceptable.
    /// </summary>
    public bool Flush(TimeSpan timeout)
    {
        // Not disposed: if the wait times out the worker may still set it later.
        var signal = new ManualResetEventSlim();
        var clock = Stopwatch.StartNew();

        if (!queue.TryAdd(new Item(default, signal), timeout))
            return false;

        var remaining = timeout - clock.Elapsed;
        return signal.Wait(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }

    /// <summary>Lets the writer thread finish what is queued and exit. The process-wide logger never calls this.</summary>
    public void Dispose() => queue.CompleteAdding();

    private void Run()
    {
        foreach (var item in queue.GetConsumingEnumerable())
        {
            if (item.FlushSignal != null)
            {
                item.FlushSignal.Set();
                continue;
            }

            try
            {
                sink(item.Value!);
            }
            catch (Exception ex)
            {
                // The sink is expected to handle its own failures; this keeps the thread alive if it doesn't.
                Debug.WriteLine($"[BackgroundWriteQueue] Sink failed: {ex}");
            }
        }
    }
}
