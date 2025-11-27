using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNetMeter.Utilities
{
    internal sealed class PeriodicWork : IDisposable, IAsyncDisposable
    {
        private readonly string name;
        private readonly TimeSpan interval;
        private readonly SemaphoreSlim stopLock = new SemaphoreSlim(1, 1);

        private Task? runTask;
        private PeriodicTimer? timer;
        private CancellationTokenSource? cts;

        public PeriodicWork(string name, TimeSpan interval)
        {
            this.name = name;
            this.interval = interval;
        }

        public void Start(Func<CancellationToken, Task> onTick)
        {
            if (runTask != null)
                return; // already running

            cts = new CancellationTokenSource();
            timer = new PeriodicTimer(interval);
            runTask = RunAsync(onTick, cts.Token, timer);
        }

        private async Task RunAsync(Func<CancellationToken, Task> onTick, CancellationToken token, PeriodicTimer timer)
        {
            try
            {
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    try
                    {
                        await onTick(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        EventLogger.Error($"{name} tick error: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"{name} task cancelled");
            }
        }

        public async Task StopAsync()
        {
            await stopLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (runTask == null)
                    return;

                cts?.Cancel();
                try
                {
                    await runTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // expected on cancellation
                }
            }
            finally
            {
                timer?.Dispose();
                cts?.Dispose();
                runTask = null;
                timer = null;
                cts = null;
                stopLock.Release();
            }
        }

        public void Stop()
        {
            StopAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Stop();
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(StopAsync());
        }
    }
}
