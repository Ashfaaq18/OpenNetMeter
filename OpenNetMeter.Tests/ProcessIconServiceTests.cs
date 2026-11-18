using Avalonia.Media;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

public class ProcessIconServiceTests
{
    private static readonly object DefaultIcon = new();

    private static WindowsProcessIconService Create(Func<string, object?> loader) =>
        new(loader, () => DefaultIcon);

    [Fact]
    public async Task LoadIconAsync_CachesResultAndGetCachedIconReturnsIt()
    {
        var calls = 0;
        var icon = new object();
        var service = Create(_ => { Interlocked.Increment(ref calls); return icon; });

        Assert.Same(DefaultIcon, service.GetCachedIcon("chrome.exe"));

        Assert.Same(icon, await service.LoadIconAsync("chrome.exe"));

        Assert.Same(icon, service.GetCachedIcon("chrome.exe"));
        // Names are matched case-insensitively and a cache hit completes without another load.
        var again = service.LoadIconAsync("CHROME.EXE");
        Assert.True(again.IsCompletedSuccessfully);
        Assert.Same(icon, await again);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task LoadIconAsync_SharesOneLoadBetweenConcurrentRequests()
    {
        var calls = 0;
        var started = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var icon = new object();
        var service = Create(_ =>
        {
            Interlocked.Increment(ref calls);
            started.Set();
            release.Wait();
            return icon;
        });

        var first = service.LoadIconAsync("chrome.exe");
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        var second = service.LoadIconAsync("chrome.exe");
        var third = service.LoadIconAsync("Chrome.exe");
        Assert.False(first.IsCompleted);

        release.Set();
        var results = await Task.WhenAll(first, second, third);

        Assert.All(results, r => Assert.Same(icon, r));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task LoadIconAsync_RunsLoadsOneAtATime()
    {
        var running = 0;
        var maxRunning = 0;
        var service = Create(name =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            Thread.Sleep(20);
            Interlocked.Decrement(ref running);
            return name;
        });

        var loads = Enumerable.Range(0, 8).Select(i => service.LoadIconAsync($"proc{i}.exe")).ToArray();
        var results = await Task.WhenAll(loads);

        Assert.Equal(Enumerable.Range(0, 8).Select(i => $"proc{i}.exe"), results);
        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task LoadIconAsync_FallsBackToTheDefaultIconWhenTheLoaderThrows()
    {
        var calls = 0;
        var service = Create(_ => { Interlocked.Increment(ref calls); throw new InvalidOperationException("boom"); });

        Assert.Same(DefaultIcon, await service.LoadIconAsync("broken.exe"));
        Assert.Same(DefaultIcon, await service.LoadIconAsync("broken.exe"));

        // The fallback is cached too, so a process whose icon can't be read is not retried on every sighting.
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task LoadIconAsync_BlankNameReturnsTheDefaultIconWithoutLoading()
    {
        var calls = 0;
        var service = Create(_ => { Interlocked.Increment(ref calls); return new object(); });

        Assert.Same(DefaultIcon, await service.LoadIconAsync(""));
        Assert.Same(DefaultIcon, service.GetCachedIcon("  "));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Attach_CachedIconIsAppliedWithoutWaiting()
    {
        var icon = new FakeImage();
        var service = new CompletedIconService(icon);
        IImage? applied = null;
        var posted = 0;

        ProcessIconLoader.Attach(service, "chrome.exe", i => applied = i, a => { posted++; a(); });

        Assert.Same(icon, applied);
        Assert.Equal(0, posted);
    }

    [Fact]
    public async Task Attach_SlowLoadIsAppliedOnTheUiThreadWhenItFinishes()
    {
        var gate = new TaskCompletionSource<object?>();
        var service = new PendingIconService(gate.Task);
        var icon = new FakeImage();
        var applied = new TaskCompletionSource<IImage?>();
        var posted = 0;

        ProcessIconLoader.Attach(service, "chrome.exe", i => applied.TrySetResult(i), a => { Interlocked.Increment(ref posted); a(); });

        // Nothing is applied (or blocked on) until the background load finishes.
        Assert.False(applied.Task.IsCompleted);

        gate.SetResult(icon);

        Assert.Same(icon, await applied.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, posted);
    }

    [Fact]
    public async Task Attach_FailedLoadKeepsTheCurrentIcon()
    {
        var gate = new TaskCompletionSource<object?>();
        var service = new PendingIconService(gate.Task);
        var applied = false;

        ProcessIconLoader.Attach(service, "chrome.exe", _ => applied = true, a => a());
        gate.SetException(new InvalidOperationException("boom"));
        await Task.Delay(50);

        Assert.False(applied);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
                return;
        }
    }

    private sealed class FakeImage : IImage
    {
        public Avalonia.Size Size => new(16, 16);
        public void Draw(DrawingContext context, Avalonia.Rect sourceRect, Avalonia.Rect destRect) { }
    }

    private sealed class CompletedIconService(object icon) : IProcessIconService
    {
        public object? GetCachedIcon(string processName) => icon;
        public Task<object?> LoadIconAsync(string processName) => Task.FromResult<object?>(icon);
    }

    private sealed class PendingIconService(Task<object?> load) : IProcessIconService
    {
        public object? GetCachedIcon(string processName) => null;
        public Task<object?> LoadIconAsync(string processName) => load;
    }
}
