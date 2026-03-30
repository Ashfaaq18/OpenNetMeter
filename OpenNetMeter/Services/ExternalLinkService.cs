using System;
using System.Diagnostics;
using OpenNetMeter.PlatformAbstractions;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Services;

public sealed class ExternalLinkService : IExternalLinkService
{
    public void Open(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Failed to open external link '{uri}'", ex);
        }
    }
}

