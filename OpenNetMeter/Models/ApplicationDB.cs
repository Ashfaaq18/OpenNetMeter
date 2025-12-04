using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DatabaseEngine;

namespace OpenNetMeter.Models
{
    /// <summary>
    /// Provides access to the application's SQLite database for storing network usage data.
    /// Uses a singleton connection pattern to prevent database locking issues.
    /// 
    /// Thread-safe: All write operations are serialized via a lock.
    /// </summary>
    internal class ApplicationDB : IDisposable
    {

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

        //---------- Singleton Connection ------------//

        private static Database? sharedDb;
        private static readonly object dbLock = new object();
        private static int refCount = 0;

        //---------- Constants ------------//

        public const string UnifiedDBFileName = "OpenNetMeter";
        public const int DataStoragePeriodInDays = 60;

        //---------- Instance State ------------//

        private readonly string adapterName;
        private bool disposed = false;

        //---------- Constructor / Disposal ------------//

        /// <summary>
        /// Creates a database accessor for the specified adapter.
        /// The underlying connection is shared across all instances.
        /// </summary>
        /// <param name="dBFileName">Adapter name (used for filtering data)</param>
        /// <param name="extraParams">Additional connection string parameters (optional)</param>
        public ApplicationDB(string dBFileName, string[]? extraParams = null)
        {
            adapterName = dBFileName;

            lock (dbLock)
            {
                if (sharedDb == null)
                {
                    Debug.WriteLine("ApplicationDB: Creating shared database connection");
                    sharedDb = new Database(Properties.Global.GetFilePath(), UnifiedDBFileName, extraParams);
                    sharedDb.ConfigureForConcurrency();
                }
                refCount++;
            }
        }

        public static string GetUnifiedDBFullPath()
        {
            return Path.Combine(Properties.Global.GetFilePath(), UnifiedDBFileName + ".sqlite");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            lock (dbLock)
            {
                refCount--;
                if (refCount == 0 && sharedDb != null)
                {
                    Debug.WriteLine("ApplicationDB: Closing shared database connection");
                    sharedDb.Dispose();
                    sharedDb = null;
                }
            }
        }

        //---------- Helper to Access Shared DB ------------//

        /// <summary>
        /// Gets the shared database instance. Throws if not initialized.
        /// </summary>
        private static Database DB
        {
            get
            {
                if (sharedDb == null)
                    throw new InvalidOperationException("Database not initialized");
                return sharedDb;
            }
        }

        //---------- Public Write Operations (Thread-Safe) ------------//

        /// <summary>
        /// Pushes process network data to the database.
        /// Thread-safe: serialized with other write operations.
        /// </summary>
        public void PushToDB(string processName, long totalDataRecv, long totalDataSend)
        {
            lock (dbLock)
            {
                InsertUniqueRow_ProcessTable(processName);
                InsertUniqueRow_AdapterTable(adapterName);

                long dateID = GetID_DateTable(DateTime.Today);
                long processID = GetID_ProcessTable(processName);
                long adapterID = GetID_AdapterTable(adapterName);

                // If insert fails (row exists), update instead
                if (InsertUniqueRow_ProcessDateTable(processID, dateID, adapterID, totalDataRecv, totalDataSend) < 1)
                {
                    UpdateRow_ProcessDateTable(processID, dateID, adapterID, totalDataRecv, totalDataSend);
                }
            }
        }

        /// <summary>
        /// Creates all required tables if they don't exist.
        /// Thread-safe: serialized with other write operations.
        /// </summary>
        public int CreateTable()
        {
            lock (dbLock)
            {
                // If any function returns negative, result will be negative
                return CreateAdapterTable() >> 31 |
                       CreateProcessTable() >> 31 |
                       CreateDateTable() >> 31 |
                       CreateProcessDateTable() >> 31;
            }
        }

        /// <summary>
        /// Inserts today's date and removes old data beyond retention period.
        /// Thread-safe: serialized with other write operations.
        /// </summary>
        public void UpdateDatesInDB()
        {
            lock (dbLock)
            {
                InsertUniqueRow_DateTable(DateTime.Today);
                RemoveOldDate();
                RemoveOldProcess();
            }
        }

        /// <summary>
        /// Ensures the adapter exists in the Adapter table.
        /// Thread-safe: serialized with other write operations.
        /// </summary>
        public int InsertUniqueRow_AdapterTable(string adapter)
        {
            lock (dbLock)
            {
                return DB.RunSQLiteNonQuery(
                    "INSERT OR IGNORE INTO Adapter(Name) VALUES(@Name)",
                    new string[,] { { "@Name", adapter } });
            }
        }

        //---------- Public Read Operations ------------//
        // Reads don't need the lock with WAL mode, but we use it for safety

        public List<List<object>> GetDataSum_ProcessDateTable(DateTime date1, DateTime date2)
        {
            lock (dbLock)
            {
                return DB.GetMultipleCellData(
                    "SELECT p1.Name, SUM(pd1.DataReceived), SUM(pd1.DataSent) " +
                    "FROM ProcessDate pd1 " +
                    "JOIN Process p1 ON p1.ID = pd1.ProcessID " +
                    "WHERE DateID IN " +
                    "(SELECT ID FROM Date WHERE " +
                    "(Year * 10000 + Month * 100 + Day) " +
                    "BETWEEN " +
                    $"({date1.Year * 10000 + date1.Month * 100 + date1.Day}) " +
                    "AND " +
                    $"({date2.Year * 10000 + date2.Month * 100 + date2.Day})) " +
                    "AND AdapterID = @AdapterID " +
                    "GROUP BY ProcessID",
                    new string[,] { { "@AdapterID", GetID_AdapterTable_Internal(adapterName).ToString() } });
            }
        }

        public (long, long) GetDataSumBetweenDates(DateTime startDate, DateTime endDate)
        {
            DateTime fromDate = startDate.Date;
            DateTime toDate = endDate.Date;

            if (toDate < fromDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            lock (dbLock)
            {
                var sum = DB.GetMultipleCellData(
                    "SELECT SUM(DataReceived), SUM(DataSent) FROM ProcessDate " +
                    "WHERE DateID IN " +
                    "(SELECT ID FROM Date " +
                    "WHERE (Year * 10000 + Month * 100 + Day) " +
                    "BETWEEN " +
                    $"({fromDate.Year * 10000 + fromDate.Month * 100 + fromDate.Day}) " +
                    "AND " +
                    $"({toDate.Year * 10000 + toDate.Month * 100 + toDate.Day})) " +
                    "AND AdapterID = @AdapterID",
                    new string[,] { { "@AdapterID", GetID_AdapterTable_Internal(adapterName).ToString() } });

                if (sum.Count == 1 && sum[0].Count == 2)
                {
                    long download = Convert.IsDBNull(sum[0][0]) ? 0 : Convert.ToInt64(sum[0][0]);
                    long upload = Convert.IsDBNull(sum[0][1]) ? 0 : Convert.ToInt64(sum[0][1]);
                    return (download, upload);
                }
            }

            return (0, 0);
        }

        public (long, long) GetTodayDataSum_ProcessDateTable()
        {
            lock (dbLock)
            {
                var sum = DB.GetMultipleCellData(
                    "SELECT SUM(DataReceived), SUM(DataSent) FROM ProcessDate " +
                    "WHERE DateID IN " +
                    "(SELECT ID FROM Date " +
                    "WHERE (Year * 10000 + Month * 100 + day) = (@Year * 10000 + @Month * 100 + @Day)) " +
                    "AND AdapterID = @AdapterID",
                    new string[,]
                    {
                        { "@Year", DateTime.Today.Year.ToString() },
                        { "@Month", DateTime.Today.Month.ToString() },
                        { "@Day", DateTime.Today.Day.ToString() },
                        { "@AdapterID", GetID_AdapterTable_Internal(adapterName).ToString() }
                    });

                if (sum.Count == 1 && sum[0].Count == 2)
                {
                    if (!Convert.IsDBNull(sum[0][0]) && !Convert.IsDBNull(sum[0][1]))
                        return (Convert.ToInt64(sum[0][0]), Convert.ToInt64(sum[0][1]));
                }
            }

            return (0, 0);
        }

        public long GetID_AdapterTable(string adapter)
        {
            lock (dbLock)
            {
                return GetID_AdapterTable_Internal(adapter);
            }
        }

        public List<string> GetAllAdapters()
        {
            var adapters = new List<string>();

            lock (dbLock)
            {
                var rows = DB.GetMultipleCellData("SELECT Name FROM Adapter ORDER BY Name");
                foreach (var row in rows)
                {
                    if (row.Count > 0 && !Convert.IsDBNull(row[0]))
                        adapters.Add(Convert.ToString(row[0])!);
                }
            }

            return adapters;
        }

        //---------- Private Table Creation ------------//
        // These are called within lock from CreateTable()

        private int CreateProcessTable()
        {
            return DB.RunSQLiteNonQuery(
                "CREATE TABLE IF NOT EXISTS Process(" +
                "ID INTEGER PRIMARY KEY NOT NULL, " +
                "Name TEXT NOT NULL UNIQUE)");
        }

        private int CreateDateTable()
        {
            return DB.RunSQLiteNonQuery(
                "CREATE TABLE IF NOT EXISTS Date(" +
                "ID INTEGER PRIMARY KEY NOT NULL, " +
                "Year INTEGER NOT NULL, " +
                "Month INTEGER NOT NULL, " +
                "Day INTEGER NOT NULL, " +
                "UNIQUE (Year, Month, Day) ON CONFLICT IGNORE)");
        }

        private int CreateProcessDateTable()
        {
            return DB.RunSQLiteNonQuery(
                "CREATE TABLE IF NOT EXISTS ProcessDate(" +
                "ID INTEGER PRIMARY KEY NOT NULL, " +
                "ProcessID INTEGER NOT NULL, " +
                "DateID INTEGER NOT NULL, " +
                "AdapterID INTEGER NOT NULL, " +
                "DataReceived INTEGER NOT NULL, " +
                "DataSent INTEGER NOT NULL, " +
                "FOREIGN KEY(ProcessID) REFERENCES Process(ID) ON DELETE CASCADE, " +
                "FOREIGN KEY(DateID) REFERENCES Date(ID) ON DELETE CASCADE, " +
                "FOREIGN KEY(AdapterID) REFERENCES Adapter(ID) ON DELETE CASCADE, " +
                "UNIQUE (ProcessID, DateID, AdapterID) ON CONFLICT IGNORE)");
        }

        private int CreateAdapterTable()
        {
            return DB.RunSQLiteNonQuery(
                "CREATE TABLE IF NOT EXISTS Adapter(" +
                "ID INTEGER PRIMARY KEY NOT NULL, " +
                "Name TEXT NOT NULL UNIQUE)");
        }

        //---------- Private Insert/Update Operations ------------//
        // These are called within lock from PushToDB() and UpdateDatesInDB()

        private int InsertUniqueRow_ProcessTable(string appName)
        {
            return DB.RunSQLiteNonQuery(
                "INSERT OR IGNORE INTO Process(Name) VALUES(@Name)",
                new string[,] { { "@Name", appName } });
        }

        private int InsertUniqueRow_DateTable(DateTime date)
        {
            return DB.RunSQLiteNonQuery(
                "INSERT OR IGNORE INTO Date(Year, Month, Day) VALUES(@Year, @Month, @Day)",
                new string[,]
                {
                    { "@Year", date.Year.ToString() },
                    { "@Month", date.Month.ToString() },
                    { "@Day", date.Day.ToString() }
                });
        }

        private void RemoveOldDate()
        {
            var cutoff = DateTime.Now.AddDays(-DataStoragePeriodInDays);
            DB.RunSQLiteNonQuery(
                "DELETE FROM Date WHERE (Year * 10000 + Month * 100 + Day) < " +
                $"({cutoff.Year * 10000 + cutoff.Month * 100 + cutoff.Day})");
        }

        private void RemoveOldProcess()
        {
            DB.RunSQLiteNonQuery(
                "DELETE FROM Process WHERE ID NOT IN " +
                "(SELECT DISTINCT ProcessID FROM ProcessDate)");
        }

        private int InsertUniqueRow_ProcessDateTable(long processID, long dateID, long adapterID, long dataReceived, long dataSent)
        {
            return DB.RunSQLiteNonQuery(
                "INSERT OR IGNORE INTO ProcessDate(ProcessID, DateID, AdapterID, DataReceived, DataSent) " +
                "VALUES(@ProcessID, @DateID, @AdapterID, @DataReceived, @DataSent)",
                new string[,]
                {
                    { "@ProcessID", processID.ToString() },
                    { "@DateID", dateID.ToString() },
                    { "@AdapterID", adapterID.ToString() },
                    { "@DataReceived", dataReceived.ToString() },
                    { "@DataSent", dataSent.ToString() }
                });
        }

        private int UpdateRow_ProcessDateTable(long processID, long dateID, long adapterID, long dataReceived, long dataSent)
        {
            return DB.RunSQLiteNonQuery(
                "UPDATE ProcessDate SET " +
                "DataReceived = DataReceived + @DataReceived, " +
                "DataSent = DataSent + @DataSent " +
                "WHERE ProcessID = @ProcessID AND DateID = @DateID AND AdapterID = @AdapterID",
                new string[,]
                {
                    { "@DataReceived", dataReceived.ToString() },
                    { "@DataSent", dataSent.ToString() },
                    { "@ProcessID", processID.ToString() },
                    { "@DateID", dateID.ToString() },
                    { "@AdapterID", adapterID.ToString() }
                });
        }

        //---------- Private ID Lookups ------------//
        // Internal versions without lock (caller must hold lock)

        private long GetID_DateTable(DateTime time)
        {
            var result = DB.GetSingleCellData(
                "SELECT ID FROM Date WHERE Year = @Year AND Month = @Month AND Day = @Day",
                new string[,]
                {
                    { "@Year", time.Year.ToString() },
                    { "@Month", time.Month.ToString() },
                    { "@Day", time.Day.ToString() }
                });

            return Convert.ToInt64(result ?? -1);
        }

        private long GetID_ProcessTable(string appName)
        {
            var result = DB.GetSingleCellData(
                "SELECT ID FROM Process WHERE Name = @Name",
                new string[,] { { "@Name", appName } });

            return Convert.ToInt64(result ?? -1);
        }

        private long GetID_AdapterTable_Internal(string adapter)
        {
            var result = DB.GetSingleCellData(
                "SELECT ID FROM Adapter WHERE Name = @Name",
                new string[,] { { "@Name", adapter } });

            return Convert.ToInt64(result ?? -1);
        }
    }
}