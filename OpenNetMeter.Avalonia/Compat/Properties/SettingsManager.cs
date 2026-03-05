using System;
using System.IO;
using System.Text.Json;

namespace OpenNetMeter.Properties;

internal static class SettingsManager
{
    private static readonly string filePath;
    public static AppSettings Current { get; } = new();

    static SettingsManager()
    {
        filePath = Path.Combine(Global.GetFilePath(), "settings.json");
        Load();
    }

    public static void Load()
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            var json = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded == null)
                return;

            Current.NetworkType = loaded.NetworkType;
            Current.NetworkSpeedFormat = loaded.NetworkSpeedFormat;
            Current.NetworkSpeedMagnitude = loaded.NetworkSpeedMagnitude;
        }
        catch
        {
        }
    }
}
