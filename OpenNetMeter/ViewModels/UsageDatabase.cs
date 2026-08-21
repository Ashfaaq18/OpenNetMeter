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
