using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

public class WlanConnectionEventTests
{
    private const string ExampleGuid = "{11111111-2222-3333-4444-555555555555}";

    private static string EventXml(string interfaceGuid, string ssid) =>
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'>" +
        "<System><Provider Name='Microsoft-Windows-WLAN-AutoConfig'/><EventID>8001</EventID></System>" +
        "<EventData>" +
        $"<Data Name='InterfaceGuid'>{interfaceGuid}</Data>" +
        "<Data Name='InterfaceDescription'>Example Wireless Adapter</Data>" +
        "<Data Name='ConnectionMode'>Automatic connection with a profile</Data>" +
        $"<Data Name='ProfileName'>{ssid}</Data>" +
        $"<Data Name='SSID'>{ssid}</Data>" +
        "<Data Name='BSSType'>Infrastructure</Data>" +
        "<Data Name='PHYType'>802.11ac</Data>" +
        "</EventData></Event>";

    [Fact]
    public void TryReadWlanConnection_ReadsInterfaceGuidAndSsid()
    {
        var ok = NetworkProcess.TryReadWlanConnection(EventXml(ExampleGuid, "ExampleNet"), out var guid, out var ssid);

        Assert.True(ok);
        Assert.Equal(Guid.Parse(ExampleGuid), guid);
        Assert.Equal("ExampleNet", ssid);
    }

    [Fact]
    public void TryReadWlanConnection_GuidMatchesNetworkInterfaceIdFormat()
    {
        NetworkProcess.TryReadWlanConnection(EventXml(ExampleGuid.ToUpperInvariant(), "ExampleNet"), out var guid, out _);

        // NetworkInterface.Id is "{GUID}"; both sides are compared as parsed Guids, not as text.
        Assert.True(Guid.TryParse(ExampleGuid.ToLowerInvariant(), out var fromAdapterId));
        Assert.Equal(fromAdapterId, guid);
    }

    [Fact]
    public void TryReadWlanConnection_KeepsSsidWithSpacesAndNonAscii()
    {
        var ok = NetworkProcess.TryReadWlanConnection(EventXml(ExampleGuid, "Example Net café"), out _, out var ssid);

        Assert.True(ok);
        Assert.Equal("Example Net café", ssid);
    }

    [Fact]
    public void TryReadWlanConnection_DifferentAdaptersYieldTheirOwnSsid()
    {
        const string otherGuid = "{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}";

        NetworkProcess.TryReadWlanConnection(EventXml(ExampleGuid, "ExampleNet"), out var firstGuid, out var firstSsid);
        NetworkProcess.TryReadWlanConnection(EventXml(otherGuid, "OtherNet"), out var secondGuid, out var secondSsid);

        Assert.NotEqual(firstGuid, secondGuid);
        Assert.Equal("ExampleNet", firstSsid);
        Assert.Equal("OtherNet", secondSsid);
    }

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("")]
    [InlineData("<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><EventData/></Event>")]
    [InlineData("<Event><EventData><Data Name='SSID'>ExampleNet</Data></EventData></Event>")]
    [InlineData("<Event><EventData><Data Name='InterfaceGuid'>{11111111-2222-3333-4444-555555555555}</Data></EventData></Event>")]
    [InlineData("<Event><EventData><Data Name='InterfaceGuid'>not-a-guid</Data><Data Name='SSID'>ExampleNet</Data></EventData></Event>")]
    [InlineData("<Event><EventData><Data Name='InterfaceGuid'>{11111111-2222-3333-4444-555555555555}</Data><Data Name='SSID'>  </Data></EventData></Event>")]
    public void TryReadWlanConnection_ReturnsFalseForMissingOrMalformedFields(string xml)
    {
        var ok = NetworkProcess.TryReadWlanConnection(xml, out var guid, out var ssid);

        Assert.False(ok);
        Assert.Equal(Guid.Empty, guid);
        Assert.Equal(string.Empty, ssid);
    }
}
