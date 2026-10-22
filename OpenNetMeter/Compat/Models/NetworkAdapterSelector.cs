using System;
using System.Collections.Generic;

namespace OpenNetMeter.Models;

/// <summary>
/// One adapter that is Up and not Loopback, reduced to the facts the selector needs.
/// <paramref name="InterfaceIndex"/> is the Windows interface index (matches GetBestInterface); null when unknown.
/// <paramref name="HasAddress"/> means a usable IPv4 or IPv6 address was found (APIPA and link-local excluded).
/// </summary>
internal sealed record AdapterCandidate(string Id, string Name, int? InterfaceIndex, bool HasGateway, bool HasAddress);

/// <summary>
/// Decides which adapter to monitor. Pure logic with no OS calls so it can be unit tested.
/// </summary>
internal static class NetworkAdapterSelector
{
    /// <summary>
    /// Picks the adapter to monitor from <paramref name="candidates"/> (adapters that are Up and not Loopback).
    ///
    /// A pinned adapter is strict: if it is missing or has no usable address the result is null
    /// (nothing is counted), with no fallback to automatic selection.
    ///
    /// Automatic prefers the adapter Windows routes internet traffic through (a gateway is not required,
    /// VPN adapters often have none), then the first adapter that has both a gateway and an address.
    /// </summary>
    public static AdapterCandidate? Select(IReadOnlyList<AdapterCandidate> candidates, string? pinnedAdapterId, int? bestRouteInterfaceIndex)
    {
        if (!string.IsNullOrEmpty(pinnedAdapterId))
        {
            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate.Id, pinnedAdapterId, StringComparison.OrdinalIgnoreCase))
                    return candidate.HasAddress ? candidate : null;
            }

            return null;
        }

        if (bestRouteInterfaceIndex.HasValue)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.InterfaceIndex == bestRouteInterfaceIndex.Value && candidate.HasAddress)
                    return candidate;
            }
        }

        foreach (var candidate in candidates)
        {
            if (candidate.HasGateway && candidate.HasAddress)
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// True when the IPv4 address can carry traffic: not unspecified (0.0.0.0) and not an
    /// APIPA self-assigned address (169.254.0.0/16, used when DHCP fails).
    /// </summary>
    public static bool IsUsableIPv4(byte[] addressBytes)
    {
        if (addressBytes.Length != 4)
            return false;

        if (addressBytes[0] == 169 && addressBytes[1] == 254)
            return false;

        return addressBytes[0] != 0 || addressBytes[1] != 0 || addressBytes[2] != 0 || addressBytes[3] != 0;
    }
}
