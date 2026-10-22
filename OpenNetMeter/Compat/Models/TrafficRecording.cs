using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenNetMeter.Models;

internal sealed record RecordingProcessUsage(string Process, long DownloadBytes, long UploadBytes);

/// <summary>
/// One user-started recording of per-process traffic: a start time, an optional stop time, the adapters
/// seen while it ran, and the bytes each process moved. Plain data with no UI or capture dependencies.
/// </summary>
internal sealed class TrafficRecording
{
    private readonly Dictionary<string, ProcessTotals> processes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> adapters = [];
    private readonly HashSet<string> adapterSet = new(StringComparer.Ordinal);

    public TrafficRecording(DateTime startedAt)
    {
        StartedAt = startedAt;
    }

    public DateTime StartedAt { get; }
    public DateTime? StoppedAt { get; private set; }
    public bool IsRunning => StoppedAt == null;
    public long TotalDownload { get; private set; }
    public long TotalUpload { get; private set; }

    /// <summary>Distinct adapter names in the order they were first seen.</summary>
    public IReadOnlyList<string> Adapters => adapters;

    public void NoteAdapter(string? adapter)
    {
        if (string.IsNullOrWhiteSpace(adapter))
            return;

        if (adapterSet.Add(adapter))
            adapters.Add(adapter);
    }

    /// <summary>Adds traffic for a process; ignored once the recording has been stopped.</summary>
    public void Add(string process, long downloadBytes, long uploadBytes)
    {
        if (!IsRunning)
            return;

        if (!processes.TryGetValue(process, out var totals))
        {
            totals = new ProcessTotals();
            processes[process] = totals;
        }

        totals.DownloadBytes += downloadBytes;
        totals.UploadBytes += uploadBytes;
        TotalDownload += downloadBytes;
        TotalUpload += uploadBytes;
    }

    /// <summary>Marks the recording as finished; calling it again keeps the first stop time.</summary>
    public void Stop(DateTime stoppedAt)
    {
        StoppedAt ??= stoppedAt;
    }

    public TimeSpan GetDuration(DateTime now)
    {
        var duration = (StoppedAt ?? now) - StartedAt;
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    /// <summary>Per-process totals, largest combined traffic first, then by name.</summary>
    public IReadOnlyList<RecordingProcessUsage> GetProcesses()
    {
        return processes
            .Select(kv => new RecordingProcessUsage(kv.Key, kv.Value.DownloadBytes, kv.Value.UploadBytes))
            .OrderByDescending(p => p.DownloadBytes + p.UploadBytes)
            .ThenBy(p => p.Process, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>"mm:ss" under an hour (e.g. "02:31"), "h:mm:ss" from an hour on (e.g. "1:05:09").</summary>
    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            return "00:00";

        long totalSeconds = (long)elapsed.TotalSeconds;
        long hours = totalSeconds / 3600;
        long minutes = totalSeconds % 3600 / 60;
        long seconds = totalSeconds % 60;

        return hours > 0
            ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, seconds)
            : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutes, seconds);
    }

    private sealed class ProcessTotals
    {
        public long DownloadBytes;
        public long UploadBytes;
    }
}
