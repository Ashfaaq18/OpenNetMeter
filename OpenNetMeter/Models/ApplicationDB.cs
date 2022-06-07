using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using DatabaseEngine;

namespace OpenNetMeter.Models
{
    internal class ApplicationDB
    {
        private Database dB;

        /// <summary>
        /// creates a database file if it does not exist
        /// </summary>
        /// <param name="dbName"></param>
        public ApplicationDB(string dbName)
        {
            dB = new Database(GetFilePath(), dbName);
        }

        private string GetFilePath()
        {
            string? appName = Assembly.GetEntryAssembly()?.GetName().Name;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (appName != null)
                path = Path.Combine(path, appName);
            else
                path = Path.Combine(path, "OpenNetMeter");

            return path;
        }

        private string TrimString(string str)
        {
            str = string.Join("", str.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            str = string.Join("", str.Split("(", StringSplitOptions.RemoveEmptyEntries));
            str = string.Join("", str.Split(")", StringSplitOptions.RemoveEmptyEntries));
            return str;
        }

        public int CreateTable(string tableName)
        {
            return dB.RunSQLiteNonQuery($"CREATE TABLE IF NOT EXISTS {TrimString(tableName)}(" +
                $"ProcessName TEXT NOT NULL, " +
                $"Date INTEGER NOT NULL, " +
                $"DataReceived INTEGER NOT NULL, " +
                $"DataSent INTEGER NOT NULL, " +
                $"PRIMARY KEY (ProcessName, Date))");
        }

        public int CreateRecord(string tableName, string appName, ulong DataRecieved, ulong DataSent)
        {
            DateTime today = DateTime.Now;
            int todayInt = today.Year * 10000 + today.Month * 100 + today.Day;
            return dB.RunSQLiteNonQuery($"INSERT OR IGNORE INTO " +
                $"{TrimString(tableName)}(ProcessName, Date, DataReceived, DataSent) " +
                $"VALUES(@ProcessName, @Date, @DataReceived, @DataSent)", 
                new string[,] {
                    {"@ProcessName", appName },
                    {"@Date",  todayInt.ToString()},
                    {"@DataReceived", DataRecieved.ToString() },
                    {"@DataSent", DataSent.ToString() }
                });
        }

        public int UpdateRecord(string tableName, string appName, ulong DataRecieved, ulong DataSent)
        {
            DateTime today = DateTime.Now;
            int todayInt = today.Year * 10000 + today.Month * 100 + today.Day;
            return dB.RunSQLiteNonQuery($"UPDATE {TrimString(tableName)} SET " +
                $"DataReceived = @DataReceived, " +
                $"DataSent = @DataSent " +
                $"WHERE " +
                $"ProcessName = @ProcessName " +
                $"AND " +
                $"Date = @Date ",
                new string[,]
                {
                    {"@DataReceived", DataRecieved.ToString() },
                    {"@DataSent", DataSent.ToString() },
                    {"@ProcessName", appName },
                    {"@Date",  todayInt.ToString()}
                });
        }

        public long GetStartDate(string tableName)
        {
            List<List<object>> test = dB.RunSQLiteReader($"SELECT " +
                $"Date " +
                $"From {TrimString(tableName)} " +
                $"ORDER BY " +
                $"Date ASC " +
                $"LIMIT 1");

            if(test.Count == 1)
            {
                if(test[0].Count == 1)
                {
                    return (long)test[0][0];
                }
            }
            return 0;
        }

        public void ReadRecord(string tableName)
        {
            List<List<object>> test =  dB.RunSQLiteReader($"SELECT * From {TrimString(tableName)}");
            for (int i = 0; i < test.Count; i++)
            {
                for(int j = 0; j< test[i].Count; j++)
                    Debug.Write(test[i][j].ToString() + ",");
                Debug.WriteLine("");
            }
                
        }
    }
}
