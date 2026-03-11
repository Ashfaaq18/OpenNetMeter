using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Properties;

internal static class SettingsManager
{
    private static readonly string filePath;
    private static readonly object sync = new();
    public static AppSettings Current { get; } = new();

    static SettingsManager()
    {
        filePath = Path.Combine(Global.GetFilePath(), "settings.json");
        Load();
    }

    public static void Load()
    {
        lock (sync)
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                var json = File.ReadAllText(filePath);
                var root = JsonNode.Parse(json) as JsonObject;
                if (root == null)
                    return;

                Current.DarkMode = GetBool(root, nameof(AppSettings.DarkMode), Current.DarkMode);
                Current.StartWithWin = GetBool(root, nameof(AppSettings.StartWithWin), Current.StartWithWin);
                Current.MinimizeOnStart = GetBool(root, nameof(AppSettings.MinimizeOnStart), Current.MinimizeOnStart);
                Current.MiniWidgetVisibility = GetBool(root, nameof(AppSettings.MiniWidgetVisibility), Current.MiniWidgetVisibility);
                Current.MiniWidgetTransparentSlider = GetInt(root, nameof(AppSettings.MiniWidgetTransparentSlider), Current.MiniWidgetTransparentSlider);
                Current.NetworkType = GetInt(root, nameof(AppSettings.NetworkType), Current.NetworkType);
                Current.NetworkSpeedFormat = GetInt(root, nameof(AppSettings.NetworkSpeedFormat), Current.NetworkSpeedFormat);
                Current.NetworkSpeedMagnitude = GetInt(root, nameof(AppSettings.NetworkSpeedMagnitude), Current.NetworkSpeedMagnitude);
            }
            catch (Exception ex)
            {
                EventLogger.Error("Error loading settings", ex);
            }
        }
    }

    public static void Save()
    {
        lock (sync)
        {
            JsonObject root;
            if (File.Exists(filePath))
            {
                try
                {
                    root = JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject ?? [];
                }
                catch (Exception ex)
                {
                    EventLogger.Error("Error parsing existing settings file before save; creating new settings root", ex);
                    root = [];
                }
            }
            else
            {
                root = [];
            }

            root[nameof(AppSettings.DarkMode)] = Current.DarkMode;
            root[nameof(AppSettings.StartWithWin)] = Current.StartWithWin;
            root[nameof(AppSettings.MinimizeOnStart)] = Current.MinimizeOnStart;
            root[nameof(AppSettings.MiniWidgetVisibility)] = Current.MiniWidgetVisibility;
            root[nameof(AppSettings.MiniWidgetTransparentSlider)] = Current.MiniWidgetTransparentSlider;
            root[nameof(AppSettings.NetworkType)] = Current.NetworkType;
            root[nameof(AppSettings.NetworkSpeedFormat)] = Current.NetworkSpeedFormat;
            root[nameof(AppSettings.NetworkSpeedMagnitude)] = Current.NetworkSpeedMagnitude;

            var json = root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
        }
    }

    private static bool GetBool(JsonObject root, string key, bool fallback)
    {
        if (!root.TryGetPropertyValue(key, out var node) || node == null)
            return fallback;

        try
        {
            return node.GetValue<bool>();
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Error reading bool setting '{key}'", ex);
            return fallback;
        }
    }

    private static int GetInt(JsonObject root, string key, int fallback)
    {
        if (!root.TryGetPropertyValue(key, out var node) || node == null)
            return fallback;

        try
        {
            return node.GetValue<int>();
        }
        catch (Exception ex)
        {
            EventLogger.Error($"Error reading int setting '{key}'", ex);
            return fallback;
        }
    }
}
