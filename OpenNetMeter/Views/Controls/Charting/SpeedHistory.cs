using System;

namespace OpenNetMeter.Views.Controls.Charting;

public readonly record struct SpeedSample(long DownloadBytesPerSecond, long UploadBytesPerSecond);

public sealed class SpeedHistory
{
    public const int VisibleSamples = 35;
    private const int Capacity = VisibleSamples + 2;

    private readonly SpeedSample[] buffer = new SpeedSample[Capacity];
    private int head;
    private int count;

    public int Count => count;

    public SpeedSample this[int index]
    {
        get
        {
            if ((uint)index >= (uint)count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return buffer[(head + index) % Capacity];
        }
    }

    public event EventHandler? SampleAppended;
    public event EventHandler? Cleared;
    public event EventHandler? DisplayFormatChanged;

    public void Append(long downloadBytesPerSecond, long uploadBytesPerSecond)
    {
        var sample = new SpeedSample(downloadBytesPerSecond, uploadBytesPerSecond);
        if (count < Capacity)
        {
            buffer[(head + count) % Capacity] = sample;
            count++;
        }
        else
        {
            buffer[head] = sample;
            head = (head + 1) % Capacity;
        }

        SampleAppended?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        head = 0;
        count = 0;
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyDisplayFormatChanged() => DisplayFormatChanged?.Invoke(this, EventArgs.Empty);

    public long MaxInWindow(int sampleCount)
    {
        long max = 0;
        int n = Math.Min(sampleCount, count);
        for (int i = count - n; i < count; i++)
        {
            var sample = this[i];
            if (sample.DownloadBytesPerSecond > max) max = sample.DownloadBytesPerSecond;
            if (sample.UploadBytesPerSecond > max) max = sample.UploadBytesPerSecond;
        }
        return max;
    }
}
