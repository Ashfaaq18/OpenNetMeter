using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNetMeter.Services;

public enum TestPhase
{
    Idle,
    Pinging,
    Downloading,
    Uploading,
    Done,
    Failed
}

public sealed class ServerInfo
{
    public string Location { get; init; } = "";
    public string Isp { get; init; } = "";
}

public sealed class SpeedTestProgress
{
    public TestPhase Phase { get; init; }
    public double LiveSpeedMbps { get; init; }
    public double? PingMs { get; init; }
    public double? DownloadMbps { get; init; }
    public double? UploadMbps { get; init; }
    public string? ErrorMessage { get; init; }
    public ServerInfo? ServerInfo { get; init; }
}

public interface ISpeedTestService
{
    Task RunAsync(IProgress<SpeedTestProgress> progress, CancellationToken ct = default);
}
