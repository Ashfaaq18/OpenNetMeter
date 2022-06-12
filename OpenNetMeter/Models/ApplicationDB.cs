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
        private int dataStoragePeriodInDays;
        /// <summary>
        /// creates a database file if it does not exist
        /// </summary>
        /// <param name="dbName"></param>
        public ApplicationDB(string dbName)
        {
            dB = new Database(GetFilePath(), TrimString(dbName));
            dataStoragePeriodInDays = 10;
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
        //                                  |       |   DataReceived|       |       |   Day         |
        //                                  |-------|---------------|       |-------|---------------|
        //                                  |       |   DataSent    |
        //                                  |-------|---------------|
        //                                  
        private int CreateProcessTable()
        {
            return dB.RunSQLiteNonQuery("CREATE TABLE IF NOT EXISTS " +
                "Process" +
                "(" +
                    "ID INTEGER PRIMARY KEY NOT NULL, " +
                    "Name TEXT NOT NULL UNIQUE" +
                ")");
        }

        private int CreateDateTable()
        {
            return dB.RunSQLiteNonQuery("CREATE TABLE IF NOT EXISTS " +
                "Date" +
                "(" +
                    "ID INTEGER PRIMARY KEY NOT NULL, " +
                    "Year INTEGER NOT NULL, " +
                    "Month INTEGER NOT NULL, " +
                    "Day INTEGER NOT NULL," +
                    "UNIQUE (Year, Month, Day) ON CONFLICT IGNORE" +
                ")");
        }

        private int CreateProcessDateTable()
        {
            return dB.RunSQLiteNonQuery("CREATE TABLE IF NOT EXISTS " +
                "ProcessDate" +
                "(" +
                    "ID INTEGER PRIMARY KEY NOT NULL, " +
                    "ProcessID INTEGER NOT NULL, " +
                    "DateID INTEGER NOT NULL, " +
                    "DataReceived INTEGER NOT NULL, " +
                    "DataSent INTEGER NOT NULL, " +
                    "FOREIGN KEY(ProcessID) REFERENCES Process(ID), " +
                    "FOREIGN KEY(DateID) REFERENCES Date(ID), " +
                    "UNIQUE (ProcessID, DateID) ON CONFLICT IGNORE" +
                ")");
        }

        public int CreateTable()
        {
            //if any one of the below function is negative, this function will return negative
            return CreateProcessTable() >> 31 | CreateDateTable() >> 31 | CreateProcessDateTable() >> 31;
        }

        public int InsertUniqueRow_ProcessTable(string appName)
        {
            return dB.RunSQLiteNonQuery("INSERT OR IGNORE INTO " +
                "Process(Name) " +
                "VALUES(@Name)",
                new string[,]
                {
                    {"@Name", appName }
                });
        }

        public int InsertUniqueRow_DateTable(DateTime date)
        {
            return dB.RunSQLiteNonQuery("INSERT OR IGNORE INTO " +
                "Date(Year, Month, Day) " +
                "VALUES(@Year, @Month, @Day)",
                new string[,]
                {
                    {"@Year", date.Year.ToString() },
                    {"@Month", date.Month.ToString() },
                    {"@Day", date.Day.ToString() }
                });
        }

        public int InsertUniqueRow_ProcessDateTable(long processID, long dateID, long dataReceived, long dataSent)
        {
            return dB.RunSQLiteNonQuery("INSERT OR IGNORE INTO " +
                "ProcessDate(ProcessID, DateID, DataReceived, DataSent) " +
                "VALUES(@ProcessID, @DateID, @DataReceived, @DataSent)",
                new string[,]
                {
                    {"@ProcessID", processID.ToString() },
                    {"@DateID", dateID.ToString() },
                    {"@DataReceived", dataReceived.ToString() },
                    {"@DataSent", dataSent.ToString() }
                });
        }

        public int UpdateRow_ProcessDateTable(long processID, long dateID, long dataReceived, long dataSent)
        {
            return dB.RunSQLiteNonQuery("UPDATE " +
                "ProcessDate " +
                "SET " +
                $"DataReceived = DataReceived + @DataReceived, " +
                $"DataSent = DataSent + @DataSent " +
                "WHERE ProcessID = @ProcessID AND DateID = @DateID", 
                new string[,]
                {
                    { "@DataReceived", dataReceived.ToString()},
                    { "@DataSent", dataSent.ToString()},
                    { "@ProcessID", processID.ToString()},
                    { "@DateID", dateID.ToString()},
                });
        }

        public int BulkInsertDateRange_DateTable(DateTime date, int rangeInDays)
        {
            List<string[]> values = new List<string[]>();

            for(int i = 0; i < Math.Abs(rangeInDays); i++)
            {
                DateTime temp = date.AddDays(i * (rangeInDays/(Math.Abs(rangeInDays))));
                values.Add(new string[] 
                {
                    temp.Year.ToString(),
                    temp.Month.ToString(),
                    temp.Day.ToString() 
                });
            }

            return dB.RunSQLiteNonQueryTransaction("INSERT OR IGNORE INTO " +
                "Date(Year, Month, Day) " +
                "Values(@Year, @Month, @Day)",
                new string[] {"Year", "Month", "Day"}, 
                values);
        }

        public long GetID_DateTable(DateTime time)
        {
            object? test = dB.GetSingleCellData("SELECT ID From " +
                "Date " +
                "WHERE " +
                $"Year = @Year " +
                "AND " +
                $"Month = @Month " +
                "AND " +
                $"Day = @Day",
                new string[,]
                {
                    {"@Year", time.Year.ToString() },
                    {"@Month", time.Month.ToString() },
                    {"@Day", time.Day.ToString() }
                });

            if (test != null)
            {
               return (long)test;
            }

            return -1;
        }
        
        public long GetID_ProcessTable(string appName)
        {
            object? test = dB.GetSingleCellData("SELECT ID From " +
                "Process " +
                "WHERE " +
                $"Name = @Name ",
                new string[,]
                {
                    {"@Name", appName}
                });


            if (test != null)
            {
                return (long)test;
            }

            return -1;
        }

        /// <summary>
        /// remove dates past the time range from date table
        /// </summary>
        public void RemoveOldDates()
        {
            DateTime time = DateTime.Now;
            time = time.AddDays(-1*dataStoragePeriodInDays);
            //DELETE FROM "main"."Date" WHERE (Year*10000 + Month * 100 + day) < (2022*10000 + 05*100 + 20)
            dB.RunSQLiteNonQuery("DELETE FROM " +
                "Date " +
                "WHERE " +
                "(Year * 10000 + Month * 100 + day) " +
                "< " +
                $"({time.Year * 10000 + time.Month * 100 + time.Day})");
        }

        //public void ReadRecord(string tableName)
        //{
        //    List<List<object>> test =  dB.RunSQLiteReader($"SELECT * From {TrimString(tableName)}");
        //    for (int i = 0; i < test.Count; i++)
        //    {
        //        for(int j = 0; j< test[i].Count; j++)
        //            Debug.Write(test[i][j].ToString() + ",");
        //        Debug.WriteLine("");
        //    }
        //}


    }
}
