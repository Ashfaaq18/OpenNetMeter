using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OpenNetMeter.ViewModels;

/// <summary>
/// Read-only access helpers for the usage database shared by the summary cards.
/// Writes always go through ApplicationDB.
/// </summary>
internal static class UsageDatabase
{
    public static string ResolveDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "OpenNetMeter");
        return Path.Combine(appFolder, "OpenNetMeter.sqlite");
    }

    /// <summary>
    /// Size on disk of the database including its write-ahead log, or 0 if it doesn't exist yet.
    /// </summary>
    public static long GetDatabaseSizeBytes()
    {
        long total = 0;
        string path = ResolveDatabasePath();
        foreach (var file in new[] { path, path + "-wal" })
        {
            try
            {
                var info = new FileInfo(file);
                if (info.Exists)
                    total += info.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Size is informational only.
            }
        }

        return total;
    }

    public static SqliteConnection OpenReadOnlyConnection(string path)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        };
        return new SqliteConnection(csb.ToString());
    }

    public static int ToDateInt(DateTime date)
    {
        return (date.Year * 10000) + (date.Month * 100) + date.Day;
    }
}
