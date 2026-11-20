using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OpenNetMeter.Utilities;

/// <summary>
/// Runs a piece of work on a background task, one run at a time, with latest-wins coalescing: requests that arrive
/// while the work is queued or running collapse into a single further run, which starts after the current one ends
/// and so sees the newest state. Used to keep a slow write (the settings file) off the UI thread without ever
/// running two of them at once and without a burst of requests turning into a burst of writes.
/// </summary>
internal sealed class LatestWinsRunner
{
    private readonly Action work;
    private readonly object gate = new();
    private bool running;
    private bool rerun;
    private Task current = Task.CompletedTask;

    public LatestWinsRunner(Action work)
    {
        this.work = work;
    }

    /// <summary>Asks for the work to run (again). Returns immediately.</summary>
    public void Request()
    {
        lock (gate)
        {
            if (running)
            {
                // The run in flight may already have read its state, so one more run is needed after it.
                rerun = true;
                return;
            }

            running = true;
            current = Task.Run(Drain);
        }
    }

    /// <summary>
    /// A task that completes once no run is queued or running (already complete when idle). For process exit and tests.
    /// </summary>
    public Task WhenIdle()
    {
        lock (gate)
            return current;
    }

    private void Drain()
    {
        while (true)
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                // The work is expected to handle its own failures; this keeps the runner usable if it doesn't.
                Debug.WriteLine($"[LatestWinsRunner] Work failed: {ex}");
            }

            lock (gate)
            {
                if (!rerun)
                {
                    running = false;
                    return;
                }

                rerun = false;
            }
        }
    }
}
