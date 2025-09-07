using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;

namespace DatabaseEngine
{
    public class Database : IDisposable
    {
        private SQLiteConnection? connection;
        private SQLiteCommand? command;
        private SQLiteTransaction? transaction;
        public Database(string path, string dbFileName, string[]? extraParams = null)
        {
            string connectionString = new Connection(path, dbFileName).ConnectionString;
            for (int i = 0; i < extraParams?.Length; i++)
            {
                connectionString += $";{extraParams[i]}";
            }
            connection = new SQLiteConnection(connectionString);
            connection?.Open(); //open the file on disk
            transaction = connection?.BeginTransaction();
            command = new SQLiteCommand(connection);
        }

        /// <summary>
        /// pass sqlite query as a string
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public int RunSQLiteNonQuery(string query)
        {
            int rowChangeCount = 0;

            try
            {
                if (command != null)
                {
                    //Debug.WriteLine(query);
                    command.CommandText = query;
                    rowChangeCount = command.ExecuteNonQuery();
                }
                else
                    rowChangeCount = -2;
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"SQLite Error: {ex.Message}");
                rowChangeCount = -1;
            }

            return rowChangeCount;
        }

        public int RunSQLiteNonQuery(string query, string[,] paramAndValue)
        {
            int rowChangeCount = 0;
            try
            {
                if (command == null) return -2;

                Debug.WriteLine(query + " ash " + paramAndValue);
                command.CommandText = query;
                for (int i = 0; i < paramAndValue.GetLength(0); i++)
                {
                    command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                }
                command.Prepare();
                rowChangeCount = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite Error: {ex.Message}");
                rowChangeCount = -1;
            }
            return rowChangeCount;
        }

        public List<List<object>> GetMultipleCellData(string query)
        {
            List<List<object>> temp = new List<List<object>>();

            try
            {
                if (command == null) return temp;

                Debug.WriteLine(query);
                command.CommandText = query;
                using SQLiteDataReader reader = command.ExecuteReader();
                if (!reader.HasRows)
                {
                    reader.Close();
                    return temp;
                }

                while (reader.Read())
                {
                    temp.Add(new List<object>());
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        temp[temp.Count - 1].Add(reader[i]);
                        Debug.WriteLine($"DB stuff, GetMultipleCellData1: {reader[i]} {reader.GetFieldType(i)}|");
                    }
                    //Debug.WriteLine("");
                }

                reader.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite read error: {ex.Message}");
            }     

            return temp;
        }
        
        public List<List<object>> GetMultipleCellData(string query, string[,] paramAndValue)
        {
            List<List<object>> temp = new List<List<object>>();

            try
            {
                if (command == null) return temp;

                command.CommandText = query;
                for (int i = 0; i < paramAndValue.GetLength(0); i++)
                {
                    command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                }
                command.Prepare();
                using SQLiteDataReader reader = command.ExecuteReader();
                if (!reader.HasRows)
                {
                    reader.Close();
                    return temp;
                }

                while (reader.Read())
                {
                    temp.Add(new List<object>());
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        temp[temp.Count - 1].Add(reader[i]);
                        Debug.WriteLine($"DB stuff, GetMultipleCellData2: {reader[i]} {reader.GetFieldType(i)}|");
                    }
                    Debug.WriteLine("");
                }
                reader.Close();

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite read error: {ex.Message}");
            }

            return temp;
        }

        public object? GetSingleCellData(string query, string[,] paramAndValue)
        {
            object? temp = null;
            try
            {
                if (command == null) return temp;

                command.CommandText = query;
                for (int i = 0; i < paramAndValue.GetLength(0); i++)
                {
                    command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                }
                command.Prepare();
                using SQLiteDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        Debug.WriteLine($"DB stuff, GetSingleCellData : {reader[0]} {reader.GetFieldType(0)}|");
                        return reader[0];
                    }
                }
                reader.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite read error: {ex.Message}");
            }
            return temp;
        }

        public void Dispose()
        {
            command?.Dispose();

            transaction?.Commit();
            transaction?.Dispose();

            connection?.Dispose();
        }
    }
}
