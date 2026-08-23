using OpenNetMeter.Views.Controls.Charting;

namespace OpenNetMeter.Tests;

public class SpeedHistoryTests
{
    [Fact]
    public void Append_TracksCountAndOrderWithinCapacity()
    {
        var history = new SpeedHistory();

        history.Append(10, 20);
        history.Append(30, 40);

        Assert.Equal(2, history.Count);
        Assert.Equal(new SpeedSample(10, 20), history[0]);
        Assert.Equal(new SpeedSample(30, 40), history[1]);
    }

    [Fact]
    public void Append_WrapsAroundOnceCapacityIsExceeded()
    {
        var history = new SpeedHistory();
        int capacity = SpeedHistory.VisibleSamples + 2;

        for (int i = 0; i < capacity + 5; i++)
            history.Append(i, i);

        Assert.Equal(capacity, history.Count);
        // Oldest retained sample is the one appended (capacity+5-capacity) = 5 ticks ago.
        Assert.Equal(new SpeedSample(5, 5), history[0]);
        Assert.Equal(new SpeedSample(capacity + 4, capacity + 4), history[history.Count - 1]);
    }

    [Fact]
    public void Clear_ResetsCountAndRaisesClearedEvent()
    {
        var history = new SpeedHistory();
        history.Append(1, 2);
        history.Append(3, 4);

        bool clearedRaised = false;
        history.Cleared += (_, _) => clearedRaised = true;

        history.Clear();

        Assert.Equal(0, history.Count);
        Assert.True(clearedRaised);
    }

    [Fact]
    public void Append_RaisesSampleAppendedEvent()
    {
        var history = new SpeedHistory();
        int raisedCount = 0;
        history.SampleAppended += (_, _) => raisedCount++;

        history.Append(1, 2);
        history.Append(3, 4);

        Assert.Equal(2, raisedCount);
    }

    [Fact]
    public void MaxInWindow_ReturnsMaxAcrossDownloadAndUpload()
    {
        var history = new SpeedHistory();
        history.Append(10, 5);
        history.Append(2, 40);
        history.Append(7, 3);

        Assert.Equal(40, history.MaxInWindow(3));
    }

    [Fact]
    public void MaxInWindow_OnlyConsidersRequestedSampleCount()
    {
        var history = new SpeedHistory();
        history.Append(100, 0);
        history.Append(1, 0);
        history.Append(2, 0);

        Assert.Equal(2, history.MaxInWindow(2));
    }

    [Fact]
    public void MaxInWindow_EmptyHistoryReturnsZero()
    {
        var history = new SpeedHistory();

        Assert.Equal(0, history.MaxInWindow(SpeedHistory.VisibleSamples));
    }

    [Fact]
    public void Indexer_OutOfRangeThrows()
    {
        var history = new SpeedHistory();
        history.Append(1, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => history[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => history[-1]);
    }
}
