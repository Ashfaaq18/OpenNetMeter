using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNetMeter.Services;

public sealed class SpeedTestService : ISpeedTestService
{
    private static readonly HttpClient client = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public async Task RunAsync(IProgress<SpeedTestProgress> progress, CancellationToken ct = default)
    {
        try
        {
            progress.Report(new SpeedTestProgress { Phase = TestPhase.Pinging });
            double pingMs = await MeasurePingAsync(ct);
            progress.Report(new SpeedTestProgress { Phase = TestPhase.Pinging, PingMs = pingMs });

            progress.Report(new SpeedTestProgress { Phase = TestPhase.Downloading, PingMs = pingMs });
            double downloadMbps = await MeasureDownloadAsync(
                liveSpeed => progress.Report(new SpeedTestProgress
                {
                    Phase = TestPhase.Downloading,
                    LiveSpeedMbps = liveSpeed,
                    PingMs = pingMs
                }),
                ct);

            progress.Report(new SpeedTestProgress
            {
                Phase = TestPhase.Downloading,
                PingMs = pingMs,
                DownloadMbps = downloadMbps
            });

            progress.Report(new SpeedTestProgress
            {
                Phase = TestPhase.Uploading,
                PingMs = pingMs,
                DownloadMbps = downloadMbps
            });
            double uploadMbps = await MeasureUploadAsync(
                liveSpeed => progress.Report(new SpeedTestProgress
                {
                    Phase = TestPhase.Uploading,
                    LiveSpeedMbps = liveSpeed,
                    PingMs = pingMs,
                    DownloadMbps = downloadMbps
                }),
                ct);

            progress.Report(new SpeedTestProgress
            {
                Phase = TestPhase.Done,
                PingMs = pingMs,
                DownloadMbps = downloadMbps,
                UploadMbps = uploadMbps
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

    private static async Task<double> MeasurePingAsync(CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            const int attempts = 3;
            double total = 0;
            int successCount = 0;

            // Warm-up
            await ping.SendPingAsync("1.1.1.1", 3000);

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

        // Fallback: HTTP round-trip time
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
        using var response = await client.GetAsync(
            "https://speed.cloudflare.com/__down?bytes=25000000",
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[65536];
        var stopwatch = Stopwatch.StartNew();
        long totalBytes = 0;
        long windowBytes = 0;
        var windowStart = stopwatch.Elapsed;

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
        {
            totalBytes += bytesRead;
            windowBytes += bytesRead;

            var now = stopwatch.Elapsed;
            if ((now - windowStart).TotalSeconds >= 0.2)
            {
                double windowSeconds = (now - windowStart).TotalSeconds;
                onLiveSpeed((windowBytes * 8.0 / 1_000_000.0) / windowSeconds);
                windowBytes = 0;
                windowStart = now;
            }
        }

        double totalSeconds = stopwatch.Elapsed.TotalSeconds;
        return totalSeconds > 0 ? (totalBytes * 8.0 / 1_000_000.0) / totalSeconds : 0;
    }

    private static async Task<double> MeasureUploadAsync(Action<double> onLiveSpeed, CancellationToken ct)
    {
        const int chunkSize = 2 * 1024 * 1024; // 2MB per chunk
        const int totalChunks = 5;             // 10MB total
        var data = new byte[chunkSize];
        new Random().NextBytes(data);

        var stopwatch = Stopwatch.StartNew();
        long totalBytes = 0;
        var lastReport = stopwatch.Elapsed;
        long lastReportBytes = 0;

        for (int i = 0; i < totalChunks; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var content = new ByteArrayContent(data);
            using var response = await client.PostAsync("https://speed.cloudflare.com/__up", content, ct);
            response.EnsureSuccessStatusCode();

            totalBytes += chunkSize;

            var now = stopwatch.Elapsed;
            double windowSeconds = (now - lastReport).TotalSeconds;
            long windowBytes = totalBytes - lastReportBytes;
            if (windowSeconds > 0)
            {
                onLiveSpeed((windowBytes * 8.0 / 1_000_000.0) / windowSeconds);
                lastReport = now;
                lastReportBytes = totalBytes;
            }
        }

        double totalSeconds = stopwatch.Elapsed.TotalSeconds;
        return totalSeconds > 0 ? (totalBytes * 8.0 / 1_000_000.0) / totalSeconds : 0;
    }
}
