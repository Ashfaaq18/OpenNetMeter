using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

public class TrafficRecordingTests
{
    private static readonly DateTime Start = new(2026, 3, 1, 14, 5, 9);

    [Fact]
    public void NewRecording_IsRunningAndEmpty()
    {
        var recording = new TrafficRecording(Start);

        Assert.True(recording.IsRunning);
        Assert.Equal(Start, recording.StartedAt);
        Assert.Null(recording.StoppedAt);
        Assert.Equal(0, recording.TotalDownload);
        Assert.Equal(0, recording.TotalUpload);
        Assert.Empty(recording.Adapters);
        Assert.Empty(recording.GetProcesses());
    }

    [Fact]
    public void Add_AccumulatesPerProcessCaseInsensitivelyAndTotals()
    {
        var recording = new TrafficRecording(Start);

        recording.Add("chrome.exe", 100, 10);
        recording.Add("Chrome.EXE", 50, 5);
        recording.Add("steam.exe", 7, 3);

        Assert.Equal(157, recording.TotalDownload);
        Assert.Equal(18, recording.TotalUpload);
        var processes = recording.GetProcesses();
        Assert.Equal(2, processes.Count);
        Assert.Equal(new RecordingProcessUsage("chrome.exe", 150, 15), processes[0]);
        Assert.Equal(new RecordingProcessUsage("steam.exe", 7, 3), processes[1]);
    }

    [Fact]
    public void Add_AfterStopIsIgnored()
    {
        var recording = new TrafficRecording(Start);
        recording.Add("chrome.exe", 100, 10);
        recording.Stop(Start.AddMinutes(1));

        recording.Add("chrome.exe", 999, 999);
        recording.Add("other.exe", 1, 1);

        Assert.Equal(100, recording.TotalDownload);
        Assert.Equal(10, recording.TotalUpload);
        Assert.Equal([new RecordingProcessUsage("chrome.exe", 100, 10)], recording.GetProcesses());
    }

    [Fact]
    public void Stop_FirstCallWins()
    {
        var recording = new TrafficRecording(Start);
        var firstStop = Start.AddMinutes(2);

        recording.Stop(firstStop);
        recording.Stop(Start.AddMinutes(10));

        Assert.False(recording.IsRunning);
        Assert.Equal(firstStop, recording.StoppedAt);
    }

    [Fact]
    public void GetDuration_UsesStoppedAtWhenStopped()
    {
        var recording = new TrafficRecording(Start);
        recording.Stop(Start.AddSeconds(151));

        Assert.Equal(TimeSpan.FromSeconds(151), recording.GetDuration(Start.AddHours(5)));
    }

    [Fact]
    public void GetDuration_UsesNowWhileRunning()
    {
        var recording = new TrafficRecording(Start);

        Assert.Equal(TimeSpan.FromSeconds(42), recording.GetDuration(Start.AddSeconds(42)));
    }

    [Fact]
    public void GetDuration_IsNeverNegative()
    {
        var recording = new TrafficRecording(Start);

        Assert.Equal(TimeSpan.Zero, recording.GetDuration(Start.AddSeconds(-30)));
    }

    [Fact]
    public void NoteAdapter_KeepsDistinctNamesInFirstSeenOrderAndIgnoresBlanks()
    {
        var recording = new TrafficRecording(Start);

        recording.NoteAdapter("WiFi");
        recording.NoteAdapter(null);
        recording.NoteAdapter("");
        recording.NoteAdapter("   ");
        recording.NoteAdapter("Ethernet");
        recording.NoteAdapter("WiFi");
        recording.NoteAdapter("Ethernet");
        recording.NoteAdapter("Hotspot");

        Assert.Equal(["WiFi", "Ethernet", "Hotspot"], recording.Adapters);
    }

    [Fact]
    public void GetProcesses_SortsByCombinedTrafficDescendingThenNameAscending()
    {
        var recording = new TrafficRecording(Start);
        recording.Add("small.exe", 1, 1);
        recording.Add("zeta.exe", 50, 50);
        recording.Add("alpha.exe", 60, 40);
        recording.Add("big.exe", 1000, 0);

        var names = recording.GetProcesses().Select(p => p.Process);

        // big (1000), then alpha and zeta tie on 100 and fall back to name, then small (2).
        Assert.Equal(["big.exe", "alpha.exe", "zeta.exe", "small.exe"], names);
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(5, "00:05")]
    [InlineData(151, "02:31")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3909, "1:05:09")]
    [InlineData(37230, "10:20:30")]
    [InlineData(-1, "00:00")]
    [InlineData(-3909, "00:00")]
    public void FormatElapsed_FormatsMinutesOrHours(int seconds, string expected)
    {
        Assert.Equal(expected, TrafficRecording.FormatElapsed(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void FormatElapsed_DropsFractionalSeconds()
    {
        Assert.Equal("00:01", TrafficRecording.FormatElapsed(TimeSpan.FromMilliseconds(1999)));
    }
}
