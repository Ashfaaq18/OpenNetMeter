using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNetMeter.Services;

public sealed class SpeedTestService : ISpeedTestService
{
    private static readonly HttpClient client = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public async Task RunAsync(IProgress<SpeedTestProgress> progress, CancellationToken ct = default)
    {
        try
        {
            progress.Report(new SpeedTestProgress { Phase = TestPhase.Pinging });

            var serverInfo = await FetchServerInfoAsync(ct);
            double pingMs = await MeasurePingAsync(ct);
            progress.Report(new SpeedTestProgress { Phase = TestPhase.Pinging, PingMs = pingMs, ServerInfo = serverInfo });

            progress.Report(new SpeedTestProgress { Phase = TestPhase.Downloading, PingMs = pingMs, ServerInfo = serverInfo });
            double downloadMbps = await MeasureDownloadAsync(
                liveSpeed => progress.Report(new SpeedTestProgress
                {
                    Phase = TestPhase.Downloading,
                    LiveSpeedMbps = liveSpeed,
                    PingMs = pingMs,
                    ServerInfo = serverInfo
                }),
                ct);

            progress.Report(new SpeedTestProgress
            {
                Phase = TestPhase.Downloading,
                PingMs = pingMs,
                DownloadMbps = downloadMbps,
                ServerInfo = serverInfo
            });

            progress.Report(new SpeedTestProgress
            {
                Phase = TestPhase.Uploading,
                PingMs = pingMs,
                DownloadMbps = downloadMbps,
                ServerInfo = serverInfo
            });
            double uploadMbps = await MeasureUploadAsync(
                liveSpeed => progress.Report(new SpeedTestProgress
                {
                    Phase = TestPhase.Uploading,
                    LiveSpeedMbps = liveSpeed,
                    PingMs = pingMs,
                    DownloadMbps = downloadMbps,
                    ServerInfo = serverInfo
                }),
                ct);

            progress.Report(new SpeedTestProgress
            {
                Phase = TestPhase.Done,
                PingMs = pingMs,
                DownloadMbps = downloadMbps,
                UploadMbps = uploadMbps,
                ServerInfo = serverInfo
            });
        }
        catch (OperationCanceledException)
        {
            progress.Report(new SpeedTestProgress { Phase = TestPhase.Idle });
        }
        catch (Exception ex)
        {
            progress.Report(new SpeedTestProgress
            {
                Phase = TestPhase.Failed,
                ErrorMessage = ex.Message
            });
        }
    }

    private static async Task<ServerInfo?> FetchServerInfoAsync(CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync("https://speed.cloudflare.com/meta", ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            string city = root.TryGetProperty("city", out var c) ? c.GetString() ?? "" : "";
            string colo = root.TryGetProperty("colo", out var co) ? co.GetString() ?? "" : "";
            string isp = root.TryGetProperty("asOrganization", out var a) ? a.GetString() ?? "" : "";

            string location = city.Length > 0 && colo.Length > 0 ? $"{city} ({colo})"
                            : city.Length > 0 ? city
                            : colo;

            return new ServerInfo { Location = location, Isp = isp };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<double> MeasurePingAsync(CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            const int attempts = 3;
            double total = 0;
            int successCount = 0;

            await ping.SendPingAsync("1.1.1.1", 3000); // warm-up

            for (int i = 0; i < attempts; i++)
            {
                ct.ThrowIfCancellationRequested();
                var reply = await ping.SendPingAsync("1.1.1.1", 3000);
                if (reply.Status == IPStatus.Success)
                {
                    total += reply.RoundtripTime;
                    successCount++;
                }
            }

            if (successCount > 0)
                return total / successCount;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { }

        // Fallback: HTTP round-trip
        try
        {
            await client.GetAsync("https://speed.cloudflare.com/__down?bytes=1", ct);
            var sw = Stopwatch.StartNew();
            await client.GetAsync("https://speed.cloudflare.com/__down?bytes=1", ct);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return 0;
        }
    }

    private static async Task<double> MeasureDownloadAsync(Action<double> onLiveSpeed, CancellationToken ct)
    {
        const int streams = 4;
        const long bytesPerStream = 25_000_000; // 25 MB × 4 = 100 MB total

        long totalBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, streams)
            .Select(_ => DownloadStreamAsync(bytesPerStream, b => Interlocked.Add(ref totalBytes, b), ct))
            .ToArray();

        using var measureCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var measure = MeasureLoopAsync(stopwatch, () => Interlocked.Read(ref totalBytes), onLiveSpeed, measureCts.Token);

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            measureCts.Cancel();
            try { await measure; } catch (OperationCanceledException) { }
        }

        double elapsed = stopwatch.Elapsed.TotalSeconds;
        return elapsed > 0 ? (Interlocked.Read(ref totalBytes) * 8.0 / 1_000_000.0) / elapsed : 0;
    }

    private static async Task<double> MeasureUploadAsync(Action<double> onLiveSpeed, CancellationToken ct)
    {
        const int streams = 4;
        const int chunksPerStream = 3;
        const int chunkSize = 2 * 1024 * 1024; // 2 MB × 3 × 4 = 24 MB total

        var data = new byte[chunkSize];
        new Random().NextBytes(data);

        long totalBytes = 0;
        var stopwatch = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, streams)
            .Select(_ => UploadStreamAsync(data, chunksPerStream, b => Interlocked.Add(ref totalBytes, b), ct))
            .ToArray();

        using var measureCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var measure = MeasureLoopAsync(stopwatch, () => Interlocked.Read(ref totalBytes), onLiveSpeed, measureCts.Token);

        try
        {
            await Task.WhenAll(tasks);
        }
        finally
        {
            measureCts.Cancel();
            try { await measure; } catch (OperationCanceledException) { }
        }

        double elapsed = stopwatch.Elapsed.TotalSeconds;
        return elapsed > 0 ? (Interlocked.Read(ref totalBytes) * 8.0 / 1_000_000.0) / elapsed : 0;
    }

    private static async Task DownloadStreamAsync(long bytes, Action<long> onBytes, CancellationToken ct)
    {
        using var response = await client.GetAsync(
            $"https://speed.cloudflare.com/__down?bytes={bytes}",
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buf = new byte[65536];
        int n;
        while ((n = await stream.ReadAsync(buf, ct)) > 0)
            onBytes(n);
    }

    private static async Task UploadStreamAsync(byte[] data, int chunks, Action<long> onBytes, CancellationToken ct)
    {
        for (int i = 0; i < chunks; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var content = new ByteArrayContent(data);
            using var response = await client.PostAsync("https://speed.cloudflare.com/__up", content, ct);
            response.EnsureSuccessStatusCode();
            onBytes(data.Length);
        }
    }

    private static async Task MeasureLoopAsync(
        Stopwatch sw,
        Func<long> getTotalBytes,
        Action<double> onLiveSpeed,
        CancellationToken ct)
    {
        long lastBytes = 0;
        var lastTime = sw.Elapsed;

        while (true)
        {
            await Task.Delay(200, ct); // throws OperationCanceledException when ct fires

            var now = sw.Elapsed;
            long cur = getTotalBytes();
            double seconds = (now - lastTime).TotalSeconds;
            long delta = cur - lastBytes;

            if (seconds > 0 && delta > 0)
                onLiveSpeed((delta * 8.0 / 1_000_000.0) / seconds);

            lastBytes = cur;
            lastTime = now;
        }
    }
}
