using Avalonia.Media;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Services;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

public class ProcessIconServiceTests
{
    private static readonly object DefaultIcon = new();

    /// <summary>
    /// Stands in for the process snapshot: maps every wanted name (no extension) to a fake path, except the names in
    /// <see cref="Missing"/> (not running), and records what each snapshot was asked for.
    /// </summary>
    private sealed class FakeSnapshot
    {
        private readonly object sync = new();
        private readonly List<HashSet<string>> requests = [];

        public HashSet<string> Missing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Action<int>? OnSnapshot { get; set; }
        public Func<IReadOnlySet<string>, IReadOnlyDictionary<string, string>>? Override { get; set; }

        public int Count
        {
            get { lock (sync) return requests.Count; }
        }

        public HashSet<string> Request(int index)
        {
            lock (sync) return requests[index];
        }

        public IReadOnlyDictionary<string, string> Take(IReadOnlySet<string> wanted)
        {
            int number;
            lock (sync)
            {
                requests.Add(new HashSet<string>(wanted, StringComparer.OrdinalIgnoreCase));
                number = requests.Count;
            }

            OnSnapshot?.Invoke(number);
            if (Override != null)
                return Override(wanted);

            return wanted.Where(name => !Missing.Contains(name)).ToDictionary(name => name, PathOf, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string PathOf(string stem) => $@"C:\fake\{stem}.exe";

    private static readonly List<string> NoLog = [];

    private static WindowsProcessIconService Create(
        Func<string, object?> loadFromPath,
        FakeSnapshot? snapshot = null,
        Func<Task>? waitForBatch = null,
        int parallelism = 3,
        List<string>? errors = null)
    {
        snapshot ??= new FakeSnapshot();
        errors ??= NoLog;
        return new WindowsProcessIconService(
            snapshot.Take,
            loadFromPath,
            () => DefaultIcon,
            waitForBatch,
            parallelism,
            (message, _) => { lock (errors) errors.Add(message); });
    }

    [Fact]
    public async Task LoadIconAsync_CachesResultAndGetCachedIconReturnsIt()
    {
        var calls = 0;
        var icon = new object();
        var snapshot = new FakeSnapshot();
        var service = Create(_ => { Interlocked.Increment(ref calls); return icon; }, snapshot);

        Assert.Same(DefaultIcon, service.GetCachedIcon("chrome.exe"));

        Assert.Same(icon, await service.LoadIconAsync("chrome.exe"));

        Assert.Same(icon, service.GetCachedIcon("chrome.exe"));
        // Names are matched case-insensitively and a cache hit completes without another load.
        var again = service.LoadIconAsync("CHROME.EXE");
        Assert.True(again.IsCompletedSuccessfully);
        Assert.Same(icon, await again);
        Assert.Equal(1, calls);
        Assert.Equal(1, snapshot.Count);
    }

    [Fact]
    public async Task LoadIconAsync_SharesOneLoadBetweenConcurrentRequests()
    {
        var calls = 0;
        var started = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var icon = new object();
        var snapshot = new FakeSnapshot();
        var service = Create(_ =>
        {
            Interlocked.Increment(ref calls);
            started.Set();
            release.Wait();
            return icon;
        }, snapshot);

        var first = service.LoadIconAsync("chrome.exe");
        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        var second = service.LoadIconAsync("chrome.exe");
        var third = service.LoadIconAsync("Chrome.exe");
        Assert.False(first.IsCompleted);

        release.Set();
        var results = await Task.WhenAll(first, second, third);

        Assert.All(results, r => Assert.Same(icon, r));
        Assert.Equal(1, calls);
        Assert.Equal(1, snapshot.Count);
    }

    [Fact]
    public async Task LoadIconAsync_RequestsMadeTogetherShareOneSnapshot()
    {
        var snapshot = new FakeSnapshot();
        var gate = new TaskCompletionSource();
        var service = Create(path => path, snapshot, waitForBatch: () => gate.Task);

        // Eight rows' worth of requests, two of them repeats (one with different letter case), before the worker starts.
        var loads = Enumerable.Range(0, 8).Select(i => service.LoadIconAsync($"proc{i}.exe")).ToList();
        loads.Add(service.LoadIconAsync("proc0.exe"));
        loads.Add(service.LoadIconAsync("PROC1.EXE"));
        gate.SetResult();
        var results = await Task.WhenAll(loads);

        Assert.Equal(1, snapshot.Count);
        Assert.Equal(Enumerable.Range(0, 8).Select(i => $"proc{i}"), snapshot.Request(0).Order());
        for (var i = 0; i < 8; i++)
            Assert.Equal(PathOf($"proc{i}"), results[i]);
        Assert.Equal(PathOf("proc0"), results[8]);
        Assert.Equal(PathOf("proc1"), results[9]);
    }

    [Fact]
    public async Task LoadIconAsync_RequestsThatArriveDuringABatchFormTheNextBatch()
    {
        var snapshot = new FakeSnapshot();
        var inSnapshot = new ManualResetEventSlim();
        var releaseSnapshot = new ManualResetEventSlim();
        snapshot.OnSnapshot = number =>
        {
            if (number != 1)
                return;
            inSnapshot.Set();
            releaseSnapshot.Wait();
        };
        var service = Create(path => path, snapshot);

        var first = service.LoadIconAsync("a.exe");
        Assert.True(inSnapshot.Wait(TimeSpan.FromSeconds(5)));
        var later = new[] { service.LoadIconAsync("b.exe"), service.LoadIconAsync("c.exe"), service.LoadIconAsync("a.exe") };
        releaseSnapshot.Set();

        await first;
        await Task.WhenAll(later);

        // The late requests wait for the batch in flight and are then resolved by one more snapshot, together.
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(["a"], snapshot.Request(0));
        Assert.Equal(["b", "c"], snapshot.Request(1).Order());
        Assert.Equal(PathOf("a"), await later[2]);
    }

    [Fact]
    public async Task LoadIconAsync_ADoneBatchIsFollowedByANewWorkerForTheNextRequest()
    {
        var snapshot = new FakeSnapshot();
        var service = Create(path => path, snapshot);

        await service.LoadIconAsync("a.exe");
        await service.LoadIconAsync("b.exe");

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(PathOf("b"), service.GetCachedIcon("b.exe"));
    }

    [Fact]
    public async Task LoadIconAsync_ExtractsIconsWithLimitedParallelism()
    {
        var running = 0;
        var maxRunning = 0;
        var twoRunning = new ManualResetEventSlim();
        var gate = new TaskCompletionSource();
        var service = Create(path =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            if (now >= 2)
                twoRunning.Set();

            // Waits for a second extraction to overlap (it does if they run in parallel), then lets go.
            twoRunning.Wait(TimeSpan.FromSeconds(5));
            Thread.Sleep(20);
            Interlocked.Decrement(ref running);
            return path;
        }, waitForBatch: () => gate.Task, parallelism: 3);

        var loads = Enumerable.Range(0, 12).Select(i => service.LoadIconAsync($"proc{i}.exe")).ToArray();
        gate.SetResult();
        var results = await Task.WhenAll(loads);

        Assert.Equal(Enumerable.Range(0, 12).Select(i => PathOf($"proc{i}")), results);
        Assert.InRange(maxRunning, 2, 3);
    }

    [Fact]
    public async Task LoadIconAsync_ParallelismOfOneRunsExtractionsOneAtATime()
    {
        var running = 0;
        var maxRunning = 0;
        var gate = new TaskCompletionSource();
        var service = Create(path =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref maxRunning, now);
            Thread.Sleep(10);
            Interlocked.Decrement(ref running);
            return path;
        }, waitForBatch: () => gate.Task, parallelism: 1);

        var loads = Enumerable.Range(0, 6).Select(i => service.LoadIconAsync($"proc{i}.exe")).ToArray();
        gate.SetResult();
        await Task.WhenAll(loads);

        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task LoadIconAsync_FallsBackToTheDefaultIconWhenTheProcessIsNotRunning()
    {
        var calls = 0;
        var snapshot = new FakeSnapshot();
        snapshot.Missing.Add("gone");
        var service = Create(_ => { Interlocked.Increment(ref calls); return new object(); }, snapshot);

        Assert.Same(DefaultIcon, await service.LoadIconAsync("gone.exe"));
        Assert.Same(DefaultIcon, await service.LoadIconAsync("gone.exe"));

        // Cached like any icon, so it isn't looked up again on every sighting; no extraction was attempted.
        Assert.Equal(1, snapshot.Count);
        Assert.Equal(0, calls);
        Assert.Same(DefaultIcon, service.GetCachedIcon("gone.exe"));
    }

    [Fact]
    public async Task LoadIconAsync_OneMissingProcessDoesNotAffectTheOthersInItsBatch()
    {
        var snapshot = new FakeSnapshot();
        snapshot.Missing.Add("gone");
        var gate = new TaskCompletionSource();
        var service = Create(path => path, snapshot, waitForBatch: () => gate.Task);

        var gone = service.LoadIconAsync("gone.exe");
        var here = service.LoadIconAsync("here.exe");
        gate.SetResult();

        Assert.Same(DefaultIcon, await gone);
        Assert.Equal(PathOf("here"), await here);
        Assert.Equal(1, snapshot.Count);
    }

    [Fact]
    public async Task LoadIconAsync_FallsBackToTheDefaultIconWhenTheLoaderThrows()
    {
        var calls = 0;
        var errors = new List<string>();
        var service = Create(_ => { Interlocked.Increment(ref calls); throw new InvalidOperationException("boom"); }, errors: errors);

        Assert.Same(DefaultIcon, await service.LoadIconAsync("broken.exe"));
        Assert.Same(DefaultIcon, await service.LoadIconAsync("broken.exe"));

        // The fallback is cached too, so a process whose icon can't be read is not retried on every sighting.
        Assert.Equal(1, calls);
        Assert.Single(errors);
    }

    [Fact]
    public async Task LoadIconAsync_ALoaderThatReturnsNothingGetsTheDefaultIcon()
    {
        var service = Create(_ => null);

        Assert.Same(DefaultIcon, await service.LoadIconAsync("blank.exe"));
    }

    [Fact]
    public async Task LoadIconAsync_OneFailingExtractionDoesNotFailTheRestOfTheBatch()
    {
        var errors = new List<string>();
        var gate = new TaskCompletionSource();
        var service = Create(path => path.Contains("broken") ? throw new InvalidOperationException("boom") : path,
            waitForBatch: () => gate.Task, errors: errors);

        var broken = service.LoadIconAsync("broken.exe");
        var fine = service.LoadIconAsync("fine.exe");
        gate.SetResult();

        Assert.Same(DefaultIcon, await broken);
        Assert.Equal(PathOf("fine"), await fine);
        Assert.Single(errors);
    }

    [Fact]
    public async Task LoadIconAsync_FallsBackToTheDefaultIconWhenTheSnapshotThrows()
    {
        var errors = new List<string>();
        var snapshot = new FakeSnapshot { Override = _ => throw new InvalidOperationException("boom") };
        var gate = new TaskCompletionSource();
        var service = Create(path => path, snapshot, waitForBatch: () => gate.Task, errors: errors);

        var loads = new[] { service.LoadIconAsync("a.exe"), service.LoadIconAsync("b.exe") };
        gate.SetResult();
        var results = await Task.WhenAll(loads);

        Assert.All(results, r => Assert.Same(DefaultIcon, r));
        Assert.Single(errors);

        // The worker survived: a later request is still served.
        snapshot.Override = null;
        Assert.Equal(PathOf("c"), await service.LoadIconAsync("c.exe"));
    }

    [Fact]
    public async Task LoadIconAsync_BlankNameReturnsTheDefaultIconWithoutLoading()
    {
        var calls = 0;
        var snapshot = new FakeSnapshot();
        var service = Create(_ => { Interlocked.Increment(ref calls); return new object(); }, snapshot);

        Assert.Same(DefaultIcon, await service.LoadIconAsync(""));
        Assert.Same(DefaultIcon, service.GetCachedIcon("  "));
        Assert.Equal(0, calls);
        Assert.Equal(0, snapshot.Count);
    }

    [Fact]
    public async Task LoadIconAsync_ANameWithoutAStemGetsTheDefaultIconWithoutASnapshot()
    {
        var snapshot = new FakeSnapshot();
        var service = Create(path => path, snapshot);

        // ".exe" has no process name to look for.
        Assert.Same(DefaultIcon, await service.LoadIconAsync(".exe"));

        Assert.Equal(0, snapshot.Count);
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
