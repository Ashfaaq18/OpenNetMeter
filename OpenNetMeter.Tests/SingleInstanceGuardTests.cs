using OpenNetMeter.Services;

namespace OpenNetMeter.Tests;

public class SingleInstanceGuardTests
{
    // Unique per test so a running OpenNetMeter (or another test) never interferes.
    private static string UniquePrefix() => $@"Local\OpenNetMeterTest-{Guid.NewGuid():N}";

    [Fact]
    public void FirstAcquireSucceedsAndSecondIsRefused()
    {
        string prefix = UniquePrefix();

        using var first = SingleInstanceGuard.TryAcquire(activateExisting: false, prefix);
        var second = SingleInstanceGuard.TryAcquire(activateExisting: false, prefix);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void AcquireSucceedsAgainAfterTheFirstCopyExits()
    {
        string prefix = UniquePrefix();

        SingleInstanceGuard.TryAcquire(activateExisting: false, prefix)!.Dispose();
        using var again = SingleInstanceGuard.TryAcquire(activateExisting: false, prefix);

        Assert.NotNull(again);
    }

    [Fact]
    public void SecondLaunchAsksRunningCopyToShowItself()
    {
        string prefix = UniquePrefix();
        using var first = SingleInstanceGuard.TryAcquire(activateExisting: false, prefix)!;
        using var activated = new ManualResetEventSlim();
        first.ListenForActivation(activated.Set);

        SingleInstanceGuard.TryAcquire(activateExisting: true, prefix);

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void StartMinimizedLaunchDoesNotActivateRunningCopy()
    {
        string prefix = UniquePrefix();
        using var first = SingleInstanceGuard.TryAcquire(activateExisting: false, prefix)!;
        using var activated = new ManualResetEventSlim();
        first.ListenForActivation(activated.Set);

        SingleInstanceGuard.TryAcquire(activateExisting: false, prefix);

        Assert.False(activated.Wait(TimeSpan.FromMilliseconds(300)));
    }
}
