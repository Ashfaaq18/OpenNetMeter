using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

public class NetworkRouteTests
{
    [Fact]
    public void SubscribeRouteChanges_ReturnsSubscriptionThatDisposesRepeatedly()
    {
        var subscription = NetworkRoute.SubscribeRouteChanges(() => { });

        Assert.NotNull(subscription);
        subscription!.Dispose();
        subscription.Dispose();
    }

    [Fact]
    public void GetBestInterfaceIndex_DoesNotThrow()
    {
        // The result depends on the machine's routing table (null when offline); this only guards the P/Invoke.
        var ex = Record.Exception(() => NetworkRoute.GetBestInterfaceIndex());

        Assert.Null(ex);
    }
}
