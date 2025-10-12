using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Properties
{
    internal static class SettingsManager
    {
        private static readonly string _filePath;

        // Create the one and only instance of our settings. 
        // The UI will bind to this instance and it will never be replaced.
        public static AppSettings Current { get; } = new AppSettings();

        static SettingsManager()
        {
            // Set the path to settings.json in the same directory as the .exe
            _filePath = Path.Combine(Global.GetFilePath(), "settings.json");
            Load();
        }

        public static void Load()
        {
            // If the file doesn't exist, we just keep the default values 
            // that 'Current' was created with.
            if (!File.Exists(_filePath))
                return;

            try
            {
                var json = File.ReadAllText(_filePath);

                // Instead of creating a new object,
                // this updates the properties of the EXISTING 'Current' object.
                JsonConvert.PopulateObject(json, Current);
            }
            catch (Exception ex)
            {
                // Log an error if the json file is corrupt, for example.
                EventLogger.Error($"Error loading settings: {ex.Message}");
            }
        }

        public static void Save()
        {
            var json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }
    }
}
