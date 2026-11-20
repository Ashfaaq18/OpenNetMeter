using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.Versioning;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Models
{
    /// <summary>
    /// Finds the SSID a Wi-Fi adapter is connected to, from the WLAN AutoConfig event log, and remembers it.
    /// Reading the log is the slow part of a network change, so an adapter's SSID is reused for as long as its
    /// addresses are unchanged, and adapters that need one together are resolved by a single pass over the log.
    /// An adapter that is no longer up is forgotten (see <see cref="Retain"/>), so a reconnect reads the log again.
    /// A lookup that finds nothing is not remembered: the connected event can land just after the address does, and
    /// the next network change should find it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class WlanSsidLookup
    {
        // How many recent "connected" events to scan for the requested adapters' SSIDs.
        internal const int MaxEventsToScan = 50;

        // The newest matching event is almost always within the first few, so don't fetch the reader's default 64.
        private const int EventBatchSize = 4;

        private readonly Func<IReadOnlySet<Guid>, IReadOnlyDictionary<Guid, string>> readSsids;
        private readonly Dictionary<Guid, CachedSsid> cache = new();
        // Held while reading, so two callers that need the same adapter share one read of the log.
        private readonly object sync = new();

        private readonly record struct CachedSsid(string AddressKey, string Ssid);

        /// <param name="readSsids">Returns the SSID of each wanted adapter it can find; one call is one read of the log.</param>
        public WlanSsidLookup(Func<IReadOnlySet<Guid>, IReadOnlyDictionary<Guid, string>> readSsids)
        {
            this.readSsids = readSsids;
        }

        /// <summary>
        /// The SSID of one adapter. <paramref name="addressKey"/> identifies the addresses the adapter has now (see
        /// <see cref="AddressKey"/>); a cached SSID is reused only while it is unchanged.
        /// </summary>
        public string? GetSsid(string adapterId, string addressKey)
        {
            if (!Guid.TryParse(adapterId, out var guid))
                return null;

            lock (sync)
            {
                if (TryGetCached(guid, addressKey, out var cached))
                    return cached;

                var found = readSsids(new HashSet<Guid> { guid });
                return Remember(guid, addressKey, found);
            }
        }

        /// <summary>
        /// The SSIDs of several adapters (keyed by adapter id), reading the log once for all of those not cached.
        /// Adapters without a result map to null.
        /// </summary>
        public Dictionary<string, string?> GetSsids(IReadOnlyCollection<(string AdapterId, string AddressKey)> adapters)
        {
            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            var missing = new List<(string AdapterId, Guid Guid, string AddressKey)>();

            lock (sync)
            {
                foreach (var (adapterId, addressKey) in adapters)
                {
                    if (!Guid.TryParse(adapterId, out var guid))
                        result[adapterId] = null;
                    else if (TryGetCached(guid, addressKey, out var cached))
                        result[adapterId] = cached;
                    else
                        missing.Add((adapterId, guid, addressKey));
                }

                if (missing.Count == 0)
                    return result;

                var found = readSsids(missing.Select(m => m.Guid).ToHashSet());
                foreach (var (adapterId, guid, addressKey) in missing)
                    result[adapterId] = Remember(guid, addressKey, found);
            }

            return result;
        }

        /// <summary>Forgets the adapters that are not in <paramref name="upAdapterIds"/>: once they come back, the log is read again.</summary>
        public void Retain(IEnumerable<string> upAdapterIds)
        {
            var up = new HashSet<Guid>();
            foreach (var id in upAdapterIds)
            {
                if (Guid.TryParse(id, out var guid))
                    up.Add(guid);
            }

            lock (sync)
            {
                foreach (var guid in cache.Keys.Where(g => !up.Contains(g)).ToList())
                    cache.Remove(guid);
            }
        }

        /// <summary>
        /// A comparable form of an adapter's usable addresses. Empty (never cached) when it has none.
        /// </summary>
        public static string AddressKey(byte[]? ipv4, byte[]? ipv6)
        {
            if (ipv4 == null && ipv6 == null)
                return string.Empty;

            return Convert.ToHexString(ipv4 ?? []) + "|" + Convert.ToHexString(ipv6 ?? []);
        }

        private bool TryGetCached(Guid guid, string addressKey, out string? ssid)
        {
            if (addressKey.Length > 0 &&
                cache.TryGetValue(guid, out var entry) &&
                string.Equals(entry.AddressKey, addressKey, StringComparison.Ordinal))
            {
                ssid = entry.Ssid;
                return true;
            }

            ssid = null;
            return false;
        }

        private string? Remember(Guid guid, string addressKey, IReadOnlyDictionary<Guid, string> found)
        {
            if (!found.TryGetValue(guid, out var ssid) || string.IsNullOrEmpty(ssid))
            {
                // Forget an older name too: it belonged to addresses the adapter no longer has.
                cache.Remove(guid);
                return null;
            }

            if (addressKey.Length > 0)
                cache[guid] = new CachedSsid(addressKey, ssid);

            return ssid;
        }

        /// <summary>
        /// Picks each wanted adapter's SSID from "connected" event XML, newest first: the first event seen for an
        /// adapter wins. Stops after <paramref name="maxEvents"/> events, or as soon as every adapter has one.
        /// </summary>
        internal static Dictionary<Guid, string> PickSsids(IEnumerable<string> eventXmlNewestFirst, IReadOnlySet<Guid> wanted, int maxEvents)
        {
            var found = new Dictionary<Guid, string>();
            if (wanted.Count == 0)
                return found;

            var scanned = 0;
            foreach (var xml in eventXmlNewestFirst)
            {
                // The named event fields are locale-independent, unlike the formatted message.
                if (NetworkProcess.TryReadWlanConnection(xml, out var interfaceGuid, out var ssid) &&
                    wanted.Contains(interfaceGuid) &&
                    !found.ContainsKey(interfaceGuid))
                {
                    found[interfaceGuid] = ssid;
                    if (found.Count == wanted.Count)
                        break;
                }

                // Checked after the event, so the one past the limit is never pulled from the log.
                if (++scanned >= maxEvents)
                    break;
            }

            return found;
        }

        /// <summary>
        /// Reads the Windows WLAN AutoConfig event log (most recent "connected" events first) for the wanted
        /// adapters' SSIDs. This is more reliable than the WinRT API on Windows 11, which needs location permission
        /// to return an SSID.
        /// </summary>
        internal static IReadOnlyDictionary<Guid, string> ReadFromEventLog(IReadOnlySet<Guid> wanted)
        {
            try
            {
                return PickSsids(ReadConnectedEventsXml(), wanted, MaxEventsToScan);
            }
            catch (Exception ex)
            {
                EventLogger.Error("Failed to read connected SSID", ex);
                return new Dictionary<Guid, string>();
            }
        }

        private static IEnumerable<string> ReadConnectedEventsXml()
        {
            // Query for EventID 8001 (successful connection) in the WLAN-AutoConfig log.
            var query = new EventLogQuery(
                "Microsoft-Windows-WLAN-AutoConfig/Operational",
                PathType.LogName,
                "*[System[EventID=8001]]"
            )
            {
                ReverseDirection = true // Get most recent first
            };

            using var reader = new EventLogReader(query) { BatchSize = EventBatchSize };
            while (true)
            {
                using EventRecord? evt = reader.ReadEvent();
                if (evt == null)
                    yield break;

                yield return evt.ToXml();
            }
        }
    }
}
