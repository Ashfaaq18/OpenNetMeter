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
            dB = new Database(GetFilePath(), TrimString(dbName));
        }

        private string TrimString(string str)
        {
            str = string.Join("", str.Split(" ", StringSplitOptions.RemoveEmptyEntries));
            str = string.Join("", str.Split("(", StringSplitOptions.RemoveEmptyEntries));
            str = string.Join("", str.Split(")", StringSplitOptions.RemoveEmptyEntries));
            return str;
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

        //
        //                                        THE ER DIAGRAM
        //                                      ------------------
        //
        //  |-----------------------|       |-----------------------|       |-----------------------|    
        //  |       Process         |       |      ProcessDate      |       |         Date          |       
        //  |-----------------------|       |-----------------------|       |-----------------------|
        //  |   PK  |   ID          |---|   |   PK  |   ID          |   |---|   PK  |   ID          |
        //  |-------|---------------|   |   |-------|---------------|   |   |-------|---------------|
        //  |       |   Name        |   |==+|   FK  |   ProcessID   |   |   |       |   Year        |
        //  |-------|---------------|       |-------|---------------|   |   |-------|---------------|
        //                                  |   FK  |   DateID      |+==|   |       |   Month       |
        //                                  |-------|---------------|       |-------|---------------|
        //                                  |       |   DataRecieved|       |       |   Day         |
        //                                  |-------|---------------|       |-------|---------------|
        //                                  |       |   DataSent    |
        //                                  |-------|---------------|
        //                                  
        private int CreateProcessTable()
        {
            return dB.RunSQLiteNonQuery($"CREATE TABLE IF NOT EXISTS Process(" +
                $"ID INTEGER PRIMARY KEY NOT NULL, " +
                $"Name TEXT NOT NULL UNIQUE)");
        }

        private int CreateDateTable()
        {
            return dB.RunSQLiteNonQuery($"CREATE TABLE IF NOT EXISTS Date(" +
                $"ID INTEGER PRIMARY KEY NOT NULL, " +
                $"Year INTEGER NOT NULL, " +
                $"Month INTEGER NOT NULL, " +
                $"Day INTEGER NOT NULL," +
                $"UNIQUE (Year, Month, Day) ON CONFLICT IGNORE)");
        }

        private int CreateProcessDateTable()
        {
            return dB.RunSQLiteNonQuery($"CREATE TABLE IF NOT EXISTS ProcessDate(" +
                $"ID INTEGER PRIMARY KEY NOT NULL, " +
                $"ProcessID INTEGER NOT NULL, " +
                $"DateID INTEGER NOT NULL, " +
                $"DataRecieved INTEGER NOT NULL, " +
                $"DataSent INTEGER NOT NULL, " +
                $"FOREIGN KEY(ProcessID) REFERENCES Process(ID), " +
                $"FOREIGN KEY(DateID) REFERENCES Date(ID))");
        }

        public int CreateTable()
        {
            //if any one of the below function is negative, this function will return negative
            return CreateProcessTable() >> 31 | CreateDateTable() >> 31 | CreateProcessDateTable() >> 31;
        }

        public int InsertUniqueProcessTableRow(string appName)
        {
            return dB.RunSQLiteNonQuery($"INSERT OR IGNORE INTO " +
                $"Process(Name) " +
                $"VALUES(@Name)",
                new string[,]
                {
                    {"@Name", appName }
                });
        }

        public int InsertUniqueDateTableRow(DateTime date)
        {
            return dB.RunSQLiteNonQuery($"INSERT OR IGNORE INTO " +
                $"Date(Year, Month, Day) " +
                $"VALUES(@Year, @Month, @Day)",
                new string[,]
                {
                    {"@Year", date.Year.ToString() },
                    {"@Month", date.Month.ToString() },
                    {"@Day", date.Day.ToString() }
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
