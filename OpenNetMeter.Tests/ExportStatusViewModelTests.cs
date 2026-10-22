using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Tests;

public class ExportStatusViewModelTests
{
    private static readonly TimeSpan ShortDelay = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task ShowSuccess_HidesItselfAfterTheDelay()
    {
        var status = new ExportStatusViewModel(new RecordingRevealService(), ShortDelay);

        status.ShowSuccess(19, @"C:\out\usage.csv");

        Assert.True(status.IsVisible);
        Assert.True(status.CanReveal);
        Assert.Equal("Exported 19 rows to", status.Message);
        Assert.Equal("usage.csv", status.FileName);
        Assert.True(await WaitUntil(() => !status.IsVisible));
        Assert.False(status.CanReveal);
        Assert.False(status.RevealCommand.CanExecute(null));
        // The text is kept so the view can fade it out rather than show it blank.
        Assert.Equal("Exported 19 rows to", status.Message);
        Assert.Equal("usage.csv", status.FileName);
    }

    [Fact]
    public void Clear_HidesAndEmptiesMessage()
    {
        var status = new ExportStatusViewModel(new RecordingRevealService(), TimeSpan.FromMinutes(1));

        status.ShowSuccess(2, @"C:\out\usage.csv");
        // Also cancels the pending auto-hide; xUnit otherwise waits for the async void delay to finish.
        status.Clear();

        Assert.False(status.IsVisible);
        Assert.Equal(string.Empty, status.Message);
        Assert.Null(status.FilePath);
    }

    [Fact]
    public async Task ShowError_StaysVisibleAndCannotReveal()
    {
        var status = new ExportStatusViewModel(new RecordingRevealService(), ShortDelay);

        status.ShowError();
        await Task.Delay(ShortDelay * 4);

        Assert.True(status.IsVisible);
        Assert.True(status.IsError);
        Assert.False(status.CanReveal);
        Assert.False(status.RevealCommand.CanExecute(null));
    }

    [Fact]
    public async Task NewStateCancelsPendingAutoHide()
    {
        var status = new ExportStatusViewModel(new RecordingRevealService(), ShortDelay);

        status.ShowSuccess(1, @"C:\out\a.csv");
        status.ShowProgress();
        await Task.Delay(ShortDelay * 4);

        Assert.True(status.IsVisible);
        Assert.Equal("Exporting...", status.Message);
    }

    [Fact]
    public void RevealCommand_OpensTheExportedFile()
    {
        var reveal = new RecordingRevealService();
        var status = new ExportStatusViewModel(reveal, TimeSpan.FromMinutes(1));

        status.ShowSuccess(3, @"C:\out\usage.json");
        status.RevealCommand.Execute(null);
        // Cancels the pending auto-hide; xUnit otherwise waits for the async void delay to finish.
        status.Clear();

        Assert.Equal([@"C:\out\usage.json"], reveal.Revealed);
    }

    private static async Task<bool> WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 100; i++)
        {
            if (condition())
                return true;
            await Task.Delay(20);
        }

        return condition();
    }

    private sealed class RecordingRevealService : IFileRevealService
    {
        public List<string> Revealed { get; } = [];
        public void Reveal(string filePath) => Revealed.Add(filePath);
    }
}
