using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

public class NetworkAdapterSelectorTests
{
    private static AdapterCandidate Adapter(string id, int? index = null, bool gateway = true, bool address = true) =>
        new(id, $"Adapter {id}", index, gateway, address);

    [Fact]
    public void Pinned_Present_IsChosenEvenWithoutGateway()
    {
        var wired = Adapter("{A}", 1);
        var vpn = Adapter("{B}", 2, gateway: false);

        var chosen = NetworkAdapterSelector.Select([wired, vpn], "{B}", bestRouteInterfaceIndex: 1);

        Assert.Same(vpn, chosen);
    }

    [Fact]
    public void Pinned_PresentWithoutAddress_ReturnsNull()
    {
        var wired = Adapter("{A}", 1);
        var vpn = Adapter("{B}", 2, gateway: false, address: false);

        Assert.Null(NetworkAdapterSelector.Select([wired, vpn], "{B}", bestRouteInterfaceIndex: 1));
    }

    [Fact]
    public void Pinned_Absent_ReturnsNullEvenIfOthersAreFine()
    {
        var wired = Adapter("{A}", 1);
        var wifi = Adapter("{C}", 3);

        Assert.Null(NetworkAdapterSelector.Select([wired, wifi], "{B}", bestRouteInterfaceIndex: 1));
    }

    [Fact]
    public void Pinned_AbsentWithNoCandidates_ReturnsNull()
    {
        Assert.Null(NetworkAdapterSelector.Select([], "{B}", bestRouteInterfaceIndex: null));
    }

    [Fact]
    public void Pinned_IdMatchIsCaseInsensitive()
    {
        var wired = Adapter("{abc-123}", 1);

        var chosen = NetworkAdapterSelector.Select([wired], "{ABC-123}", bestRouteInterfaceIndex: null);

        Assert.Same(wired, chosen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyPinnedId_MeansAutomatic(string? pinned)
    {
        var first = Adapter("{A}", 1);
        var second = Adapter("{B}", 2);

        var chosen = NetworkAdapterSelector.Select([first, second], pinned, bestRouteInterfaceIndex: 2);

        Assert.Same(second, chosen);
    }

    [Fact]
    public void Automatic_BestRouteAdapterIsChosenEvenWithoutGateway()
    {
        var wired = Adapter("{A}", 1);
        var vpn = Adapter("{B}", 2, gateway: false);

        var chosen = NetworkAdapterSelector.Select([wired, vpn], null, bestRouteInterfaceIndex: 2);

        Assert.Same(vpn, chosen);
    }

    [Fact]
    public void Automatic_BestRouteNotMatchingAnyCandidate_FallsBackToFirstWithGatewayAndAddress()
    {
        var noGateway = Adapter("{A}", 1, gateway: false);
        var wired = Adapter("{B}", 2);
        var wifi = Adapter("{C}", 3);

        var chosen = NetworkAdapterSelector.Select([noGateway, wired, wifi], null, bestRouteInterfaceIndex: 99);

        Assert.Same(wired, chosen);
    }

    [Fact]
    public void Automatic_BestRouteNull_UsesFirstWithGatewayAndAddressInOrder()
    {
        var noAddress = Adapter("{A}", 1, address: false);
        var wired = Adapter("{B}", 2);
        var wifi = Adapter("{C}", 3);

        var chosen = NetworkAdapterSelector.Select([noAddress, wired, wifi], null, bestRouteInterfaceIndex: null);

        Assert.Same(wired, chosen);
    }

    [Fact]
    public void Automatic_BestRouteAdapterWithoutAddress_FallsBackToGatewayRule()
    {
        var vpn = Adapter("{A}", 1, gateway: false, address: false);
        var wired = Adapter("{B}", 2);

        var chosen = NetworkAdapterSelector.Select([vpn, wired], null, bestRouteInterfaceIndex: 1);

        Assert.Same(wired, chosen);
    }

    [Fact]
    public void Automatic_CandidatesWithUnknownIndex_AreNotMatchedByBestRoute()
    {
        var unknownIndex = Adapter("{A}", index: null, gateway: false);
        var wired = Adapter("{B}", 2);

        var chosen = NetworkAdapterSelector.Select([unknownIndex, wired], null, bestRouteInterfaceIndex: 0);

        Assert.Same(wired, chosen);
    }

    [Fact]
    public void Automatic_NothingSuitable_ReturnsNull()
    {
        var noGateway = Adapter("{A}", 1, gateway: false);
        var noAddress = Adapter("{B}", 2, address: false);

        Assert.Null(NetworkAdapterSelector.Select([noGateway, noAddress], null, bestRouteInterfaceIndex: 5));
        Assert.Null(NetworkAdapterSelector.Select([], null, bestRouteInterfaceIndex: null));
    }

    [Theory]
    [InlineData(new byte[] { 192, 168, 1, 50 }, true)]
    [InlineData(new byte[] { 10, 0, 0, 5 }, true)]
    [InlineData(new byte[] { 169, 253, 1, 1 }, true)]
    [InlineData(new byte[] { 169, 254, 12, 34 }, false)]
    [InlineData(new byte[] { 0, 0, 0, 0 }, false)]
    [InlineData(new byte[] { 192, 168, 1 }, false)]
    public void IsUsableIPv4_RejectsApipaAndUnspecified(byte[] address, bool expected)
    {
        Assert.Equal(expected, NetworkAdapterSelector.IsUsableIPv4(address));
    }
}
