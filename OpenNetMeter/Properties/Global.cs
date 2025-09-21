using System;
using System.IO;
using System.Reflection;

namespace OpenNetMeter.Properties
{
    internal class Global
    {
        public const string AppName = "OpenNetMeter";

        public static string GetFilePath()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, AppName);
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
