using OpenNetMeter.Models;

namespace OpenNetMeter.Tests;

/// <summary>
/// The SSID cache in front of the WLAN event log. The log itself is replaced by a fake source that records each
/// read, so nothing here touches the real Event Log.
/// </summary>
public class WlanSsidLookupTests
{
    private const string WifiId = "{11111111-2222-3333-4444-555555555555}";
    private const string OtherWifiId = "{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}";

    private static readonly byte[] Address1 = [192, 168, 1, 20];
    private static readonly byte[] Address2 = [192, 168, 1, 21];

    private sealed class FakeLog
    {
        public Dictionary<Guid, string> Ssids { get; } = new();
        public List<HashSet<Guid>> Reads { get; } = [];

        public IReadOnlyDictionary<Guid, string> Read(IReadOnlySet<Guid> wanted)
        {
            Reads.Add([.. wanted]);
            return Ssids.Where(entry => wanted.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value);
        }
    }

    private static string Key(byte[]? ipv4, byte[]? ipv6 = null) => WlanSsidLookup.AddressKey(ipv4, ipv6);

    private static string EventXml(string interfaceGuid, string ssid) =>
        "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><EventData>" +
        $"<Data Name='InterfaceGuid'>{interfaceGuid}</Data><Data Name='SSID'>{ssid}</Data>" +
        "</EventData></Event>";

    [Fact]
    public void GetSsid_SecondCallWithTheSameAddressesDoesNotReadTheLogAgain()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        var lookup = new WlanSsidLookup(log.Read);

        Assert.Equal("HomeNet", lookup.GetSsid(WifiId, Key(Address1)));
        Assert.Equal("HomeNet", lookup.GetSsid(WifiId, Key(Address1)));
        // The same adapter, written with different letter case, is the same adapter.
        Assert.Equal("HomeNet", lookup.GetSsid(WifiId.ToUpperInvariant(), Key(Address1)));

        Assert.Single(log.Reads);
    }

    [Fact]
    public void GetSsid_ReadsAgainWhenTheAddressesChange()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        var lookup = new WlanSsidLookup(log.Read);
        Assert.Equal("HomeNet", lookup.GetSsid(WifiId, Key(Address1)));

        log.Ssids[Guid.Parse(WifiId)] = "CafeNet";

        Assert.Equal("CafeNet", lookup.GetSsid(WifiId, Key(Address2)));
        Assert.Equal(2, log.Reads.Count);
        // The new addresses are cached in turn.
        Assert.Equal("CafeNet", lookup.GetSsid(WifiId, Key(Address2)));
        Assert.Equal(2, log.Reads.Count);
    }

    [Fact]
    public void GetSsid_IPv6ChangeAloneInvalidates()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        var lookup = new WlanSsidLookup(log.Read);

        lookup.GetSsid(WifiId, Key(Address1, [0x20, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]));
        lookup.GetSsid(WifiId, Key(Address1, [0x20, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2]));

        Assert.Equal(2, log.Reads.Count);
    }

    [Fact]
    public void GetSsid_DoesNotRememberAMissingSsid()
    {
        var log = new FakeLog();
        var lookup = new WlanSsidLookup(log.Read);

        // The connected event can arrive after the address does; the next network change must look again.
        Assert.Null(lookup.GetSsid(WifiId, Key(Address1)));
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";

        Assert.Equal("HomeNet", lookup.GetSsid(WifiId, Key(Address1)));
        Assert.Equal(2, log.Reads.Count);
    }

    [Fact]
    public void GetSsid_WithoutAddressesIsNeverCached()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        var lookup = new WlanSsidLookup(log.Read);

        Assert.Equal(string.Empty, Key(null, null));
        lookup.GetSsid(WifiId, Key(null));
        lookup.GetSsid(WifiId, Key(null));

        Assert.Equal(2, log.Reads.Count);
    }

    [Fact]
    public void GetSsid_NotAGuidReturnsNullWithoutReading()
    {
        var log = new FakeLog();
        var lookup = new WlanSsidLookup(log.Read);

        Assert.Null(lookup.GetSsid("not-a-guid", Key(Address1)));

        Assert.Empty(log.Reads);
    }

    [Fact]
    public void Retain_ForgetsAdaptersThatWentDownSoAReconnectReadsAgain()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        var lookup = new WlanSsidLookup(log.Read);
        lookup.GetSsid(WifiId, Key(Address1));

        // The adapter was not up in the latest enumeration...
        lookup.Retain([OtherWifiId]);
        log.Ssids[Guid.Parse(WifiId)] = "CafeNet";

        // ...so coming back with the very same address still re-reads.
        Assert.Equal("CafeNet", lookup.GetSsid(WifiId, Key(Address1)));
        Assert.Equal(2, log.Reads.Count);
    }

    [Fact]
    public void Retain_KeepsAdaptersThatAreStillUp()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        var lookup = new WlanSsidLookup(log.Read);
        lookup.GetSsid(WifiId, Key(Address1));

        lookup.Retain([WifiId, OtherWifiId]);
        lookup.GetSsid(WifiId, Key(Address1));

        Assert.Single(log.Reads);
    }

    [Fact]
    public void GetSsids_ResolvesSeveralAdaptersWithOneRead()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        log.Ssids[Guid.Parse(OtherWifiId)] = "OfficeNet";
        var lookup = new WlanSsidLookup(log.Read);

        var ssids = lookup.GetSsids([(WifiId, Key(Address1)), (OtherWifiId, Key(Address2))]);

        Assert.Equal("HomeNet", ssids[WifiId]);
        Assert.Equal("OfficeNet", ssids[OtherWifiId]);
        var read = Assert.Single(log.Reads);
        Assert.Equal(2, read.Count);
        Assert.Contains(Guid.Parse(WifiId), read);
        Assert.Contains(Guid.Parse(OtherWifiId), read);
    }

    [Fact]
    public void GetSsids_OnlyReadsForAdaptersThatAreNotCached()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        log.Ssids[Guid.Parse(OtherWifiId)] = "OfficeNet";
        var lookup = new WlanSsidLookup(log.Read);
        lookup.GetSsid(WifiId, Key(Address1));

        var ssids = lookup.GetSsids([(WifiId, Key(Address1)), (OtherWifiId, Key(Address2))]);

        Assert.Equal("HomeNet", ssids[WifiId]);
        Assert.Equal("OfficeNet", ssids[OtherWifiId]);
        Assert.Equal(2, log.Reads.Count);
        Assert.Equal([Guid.Parse(OtherWifiId)], log.Reads[1]);

        // Everything is cached now, so a further call reads nothing.
        lookup.GetSsids([(WifiId, Key(Address1)), (OtherWifiId, Key(Address2))]);
        Assert.Equal(2, log.Reads.Count);
    }

    [Fact]
    public void GetSsids_UnknownAdaptersMapToNull()
    {
        var log = new FakeLog();
        log.Ssids[Guid.Parse(WifiId)] = "HomeNet";
        var lookup = new WlanSsidLookup(log.Read);

        var ssids = lookup.GetSsids([(WifiId, Key(Address1)), (OtherWifiId, Key(Address2)), ("not-a-guid", Key(Address2))]);

        Assert.Equal("HomeNet", ssids[WifiId]);
        Assert.Null(ssids[OtherWifiId]);
        Assert.Null(ssids["not-a-guid"]);
    }

    [Fact]
    public void GetSsids_WithNoAdaptersDoesNotRead()
    {
        var log = new FakeLog();
        var lookup = new WlanSsidLookup(log.Read);

        Assert.Empty(lookup.GetSsids([]));

        Assert.Empty(log.Reads);
    }

    [Fact]
    public void PickSsids_TakesTheNewestEventPerAdapterInOnePass()
    {
        var wanted = new HashSet<Guid> { Guid.Parse(WifiId), Guid.Parse(OtherWifiId) };
        string[] newestFirst =
        [
            EventXml(WifiId, "NewHome"),
            EventXml("{99999999-9999-9999-9999-999999999999}", "Unrelated"),
            EventXml(OtherWifiId, "OfficeNet"),
            EventXml(WifiId, "OldHome"),
        ];

        var found = WlanSsidLookup.PickSsids(newestFirst, wanted, maxEvents: 50);

        Assert.Equal("NewHome", found[Guid.Parse(WifiId)]);
        Assert.Equal("OfficeNet", found[Guid.Parse(OtherWifiId)]);
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void PickSsids_StopsReadingOnceEveryAdapterHasAnSsid()
    {
        var wanted = new HashSet<Guid> { Guid.Parse(WifiId) };
        var pulled = 0;

        IEnumerable<string> Events()
        {
            pulled++;
            yield return EventXml(WifiId, "HomeNet");
            pulled++;
            yield return EventXml(WifiId, "Older");
        }

        WlanSsidLookup.PickSsids(Events(), wanted, maxEvents: 50);

        Assert.Equal(1, pulled);
    }

    [Fact]
    public void PickSsids_ScansAtMostTheGivenNumberOfEvents()
    {
        var wanted = new HashSet<Guid> { Guid.Parse(WifiId) };
        var pulled = 0;

        IEnumerable<string> Events()
        {
            for (var i = 0; i < 100; i++)
            {
                pulled++;
                yield return EventXml(OtherWifiId, "Other");
            }
        }

        var found = WlanSsidLookup.PickSsids(Events(), wanted, maxEvents: 5);

        Assert.Empty(found);
        Assert.Equal(5, pulled);
    }

    [Fact]
    public void PickSsids_SkipsMalformedEvents()
    {
        var wanted = new HashSet<Guid> { Guid.Parse(WifiId) };

        var found = WlanSsidLookup.PickSsids(["not xml", EventXml(WifiId, "HomeNet")], wanted, maxEvents: 50);

        Assert.Equal("HomeNet", found[Guid.Parse(WifiId)]);
    }
}
