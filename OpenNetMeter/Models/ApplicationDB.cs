using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using DatabaseEngine;

namespace OpenNetMeter.Models
{
    internal class ApplicationDB : IDisposable
    {
        private Database dB;
        public const int DataStoragePeriodInDays = 60;
        /// <summary>
        /// creates a database file if it does not exist
        /// </summary>
        /// <param name="dbName"></param>
        public ApplicationDB(string dBFileName, string[]? extraParams = null)
        {
            dB = new Database(GetFilePath(), dBFileName, extraParams);

        }

        public static string GetFilePath()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "user data");
            Directory.CreateDirectory(path);
            return path;
        }

        public void PushToDB(string processName, long totalDataRecv, long totalDataSend, string adapterName)
        {
            Debug.WriteLine($"Pushing to DB: {processName} | {totalDataRecv} | {totalDataSend} | {adapterName}");
            InsertUniqueRow_ProcessTable(processName);
            InsertUniqueRow_AdapterTable(adapterName);

            long dateID = GetID_DateTable(DateTime.Today);
            long processID = GetID_ProcessTable(processName);
            long adapterID = GetID_AdapterTable(adapterName);

            //if the current process is in the ProcessDate table, update that row. else, insert a new row with the accumulated data values
            if (InsertUniqueRow_ProcessDateTable(processID, dateID, adapterID, totalDataRecv, totalDataSend) < 1)
            {
                UpdateRow_ProcessDateTable(processID, dateID, adapterID, totalDataRecv, totalDataSend);
            }
        }

        //
        //                                        THE ER DIAGRAM
        //                                      ------------------
        //
        //  |-----------------------|       |-----------------------|       |-----------------------|       |-----------------------|    
        //  |       Adapter         |       |       Process         |       |      ProcessDate      |       |         Date          |       
        //  |-----------------------|       |-----------------------|       |-----------------------|       |-----------------------|
        //  |   PK  |   ID          |---|   |   PK  |   ID          |---|   |   PK  |   ID          |   |---|   PK  |   ID          |
        //  |-------|---------------|   |   |-------|---------------|   |   |-------|---------------|   |   |-------|---------------|  
        //  |       |   Name        |   |   |       |   Name        |   |==+|   FK  |   ProcessID   |   |   |       |   Year        |
        //  |-------|---------------|   |   |-------|---------------|       |-------|---------------|   |   |-------|---------------|
        //                              |                                   |   FK  |   DateID      |+==|   |       |   Month       |
        //                              |                                   |-------|---------------|       |-------|---------------|       
        //                              |==================================+|   FK  |   AdapterID   |       |       |   Day         |
        //                                                                  |-------|---------------|       |-------|---------------|      
        //                                                                  |       |   DataReceived|
        //                                                                  |-------|---------------|
        //                                                                  |       |   DataSent    |
        //                                                                  |-------|---------------| 
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

        private int CreateAdapterTable()
        {
            return dB.RunSQLiteNonQuery("CREATE TABLE IF NOT EXISTS " +
                "Adapter" +
                "(" +
                    "ID INTEGER PRIMARY KEY NOT NULL, " +
                    "Name TEXT NOT NULL UNIQUE" +
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
                    "AdapterID INTEGER NOT NULL, " +
                    "DataReceived INTEGER NOT NULL, " +
                    "DataSent INTEGER NOT NULL, " +
                    "FOREIGN KEY(ProcessID) REFERENCES Process(ID) ON DELETE CASCADE, " +
                    "FOREIGN KEY(DateID) REFERENCES Date(ID) ON DELETE CASCADE, " +
                    "FOREIGN KEY(AdapterID) REFERENCES Adapter(ID) ON DELETE CASCADE, " +
                    "UNIQUE (ProcessID, DateID, AdapterID) ON CONFLICT IGNORE" +
                ")");
        }

        public int CreateTable()
        {
            // Add Adapter table creation
            return CreateProcessTable() >> 31 | CreateDateTable() >> 31 | CreateAdapterTable() >> 31 | CreateProcessDateTable() >> 31;
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

        private int InsertUniqueRow_DateTable(DateTime date)
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

        private void RemoveOldDate()
        {
            DateTime time = DateTime.Now;
            time = time.AddDays(-1 * DataStoragePeriodInDays);

            dB.RunSQLiteNonQuery("DELETE FROM " +
                "Date " +
                "WHERE " +
                "(Year * 10000 + Month * 100 + day) " +
                "< " +
                $"({time.Year * 10000 + time.Month * 100 + time.Day})");
        }

        private void RemoveOldProcess()
        {
            dB.RunSQLiteNonQuery("DELETE FROM " +
                "Process WHERE ID IN " +
                "(SELECT ID FROM Process WHERE ID NOT IN " +
                "(SELECT DISTINCT ProcessID FROM ProcessDate))");
        }

        public void UpdateDatesInDB()
        {
            //insert todays date
            InsertUniqueRow_DateTable(DateTime.Today);

            //get invalid dateIDs
            //use these invalid dataIDs to remove the respective rows from processDate table
            //remove these IDs from date table
            RemoveOldDate();
            //compare processDate table processID column and process table processID column
            //remove rows from process table if they are not present in processDate table
            RemoveOldProcess();
        }

        public int InsertUniqueRow_ProcessDateTable(long processID, long dateID, long adapterID, long dataReceived, long dataSent)
        {
            return dB.RunSQLiteNonQuery("INSERT OR IGNORE INTO " +
                "ProcessDate(ProcessID, DateID, AdapterID, DataReceived, DataSent) " +
                "VALUES(@ProcessID, @DateID, @AdapterID, @DataReceived, @DataSent)",
                new string[,]
                {
                    {"@ProcessID", processID.ToString() },
                    {"@DateID", dateID.ToString() },
                    {"@AdapterID", adapterID.ToString() },
                    {"@DataReceived", dataReceived.ToString() },
                    {"@DataSent", dataSent.ToString() }
                });
        }

        public int UpdateRow_ProcessDateTable(long processID, long dateID, long adapterID, long dataReceived, long dataSent)
        {
            return dB.RunSQLiteNonQuery("UPDATE " +
                "ProcessDate " +
                "SET " +
                $"DataReceived = DataReceived + @DataReceived, " +
                $"DataSent = DataSent + @DataSent " +
                "WHERE ProcessID = @ProcessID AND DateID = @DateID AND AdapterID = @AdapterID", 
                new string[,]
                {
                    { "@DataReceived", dataReceived.ToString()},
                    { "@DataSent", dataSent.ToString()},
                    { "@ProcessID", processID.ToString()},
                    { "@DateID", dateID.ToString()},
                    { "@AdapterID", adapterID.ToString()},
                });
        }

        public List<List<object>> GetDataSum_ProcessDateTable(DateTime date1, DateTime date2, string? adapterName)
        {
            string query =
                "SELECT p1.Name, SUM(pd1.DataReceived), SUM(pd1.DataSent) " +
                "FROM ProcessDate pd1 " +
                "JOIN Process p1 ON p1.ID = pd1.ProcessID " +
                "JOIN Adapter a1 ON a1.ID = pd1.AdapterID " +
                "WHERE DateID IN " +
                "(SELECT ID FROM Date WHERE " +
                "(Year * 10000 + Month * 100 + Day) " +
                "BETWEEN (@StartDate) AND (@EndDate)) " +
                (adapterName != null ? "AND a1.Name = @AdapterName " : "") +
                "GROUP BY ProcessID";

            var paramList = new List<(string, string)>
            {
                ("@StartDate", (date1.Year * 10000 + date1.Month * 100 + date1.Day).ToString()),
                ("@EndDate", (date2.Year * 10000 + date2.Month * 100 + date2.Day).ToString())
            };
            if (adapterName != null)
                paramList.Add(("@AdapterName", adapterName));

            string[,] paramArray = new string[paramList.Count, 2];
            for (int i = 0; i < paramList.Count; i++)
            {
                paramArray[i, 0] = paramList[i].Item1;
                paramArray[i, 1] = paramList[i].Item2;
            }

            return dB.GetMultipleCellData(query, paramArray);
        }

        public (long, long) GetTodayDataSum_ProcessDateTable()
        {
            List<List<object>> sum = dB.GetMultipleCellData("SELECT SUM(DataReceived), SUM(DataSent) FROM ProcessDate " +
                "WHERE DateID IN " +
                "(SELECT ID FROM Date " +
                "WHERE (Year * 10000 + Month * 100 + day) = (@Year * 10000 + @Month * 100 + @Day))",
                new string[,]
                {
                    { "@Year", DateTime.Today.Year.ToString()},
                    { "@Month", DateTime.Today.Month.ToString()},
                    { "@Day", DateTime.Today.Day.ToString()}
                });

            if(sum.Count == 1)
            {
                if (sum[0].Count == 2)
                {
                    if (!Convert.IsDBNull(sum[0][0]) && !Convert.IsDBNull(sum[0][1]))
                        return (Convert.ToInt64(sum[0][0]), Convert.ToInt64(sum[0][1]));
                }
            }

            return (0,0);
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

            return Convert.ToInt64(test ?? -1);
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

            return Convert.ToInt64(test ?? -1);
        }

        public int InsertUniqueRow_AdapterTable(string adapterName)
        {
            return dB.RunSQLiteNonQuery("INSERT OR IGNORE INTO " +
                "Adapter(Name) " +
                "VALUES(@Name)",
                new string[,]
                {
                    {"@Name", adapterName }
                });
        }

        public long GetID_AdapterTable(string adapterName)
        {
            object? test = dB.GetSingleCellData("SELECT ID From " +
                "Adapter " +
                "WHERE " +
                $"Name = @Name ",
                new string[,]
                {
                    {"@Name", adapterName}
                });

            return Convert.ToInt64(test ?? -1);
        }

        public void Dispose()
        {
            dB?.Dispose();
        }

        public List<string> GetAllAdapters()
        {
            var result = dB.GetMultipleCellData("SELECT Name FROM Adapter");
            return result.Select(row => row[0]?.ToString() ?? "").Where(name => !string.IsNullOrEmpty(name)).ToList();
        }
    }
}
