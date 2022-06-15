using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace DatabaseEngine
{
    public class Database : IDisposable
    {
        private SQLiteConnection? connection;
        private SQLiteCommand? command;
        public Database(string path, string dbFileName)
        {
            connection = new SQLiteConnection(new Connection(path, dbFileName).ConnectionString);
            if(connection != null)
            {
                connection.Open();
                command = new SQLiteCommand(connection);
            }
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
                if (command != null)
                {
                    //Debug.WriteLine(query);
                    command.CommandText = query;
                    for (int i = 0; i < paramAndValue.GetLength(0); i++)
                    {
                        command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                    }
                    command.Prepare();
                    rowChangeCount = command.ExecuteNonQuery();
                }
                else
                    rowChangeCount = -2;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite Error: {ex.Message}");
                rowChangeCount = -1;
            }
            return rowChangeCount;
        }

        public int RunSQLiteNonQueryTransaction(string query, string[] columns, List<string[]> values)
        {
            int rowChangeCount = 0;
            try
            {
                if (connection != null && command != null)
                {
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        if (columns.GetLength(0) == values[0].GetLength(0))
                        {
                            for (int i = 0; i < values.Count; i++)
                            {
                                command.CommandText = query;
                                for (int j = 0; j < columns.GetLength(0); j++)
                                {
                                    command.Parameters.AddWithValue($"@{columns[j]}", values[i][j]);
                                    //Debug.Write(values[i][j] + ",");
                                }
                                //Debug.WriteLine("");
                                command.Prepare();
                                rowChangeCount += command.ExecuteNonQuery();
                            }
                            transaction.Commit();
                        }
                    }
                }
                else
                    rowChangeCount = -2;
                
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
            using (SQLiteConnection connection = new SQLiteConnection(""))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    try
                    {
                        command.CommandText = query;
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    temp.Add(new List<object>());
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        temp[temp.Count - 1].Add(reader[i]);
                                        Debug.Write($"{reader[i]} {reader.GetFieldType(i)}|");
                                    }
                                    Debug.WriteLine("");
                                }
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SQLite read error: {ex.Message}");
                    }
                }
                connection.Close();
            }
            return temp;
        }
        
        public List<List<object>> GetMultipleCellData(string query, string[,] paramAndValue)
        {
            List<List<object>> temp = new List<List<object>>();
            using (SQLiteConnection connection = new SQLiteConnection(""))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    try
                    {
                        command.CommandText = query;
                        for (int i = 0; i < paramAndValue.GetLength(0); i++)
                        {
                            command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                        }
                        command.Prepare();
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    temp.Add(new List<object>());
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        temp[temp.Count - 1].Add(reader[i]);
                                        Debug.Write($"{reader[i]} {reader.GetFieldType(i)}|");
                                    }
                                    Debug.WriteLine("");
                                }
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SQLite read error: {ex.Message}");
                    }
                }
                connection.Close();
            }
            return temp;
        }

        public object? GetSingleCellData(string query)
        {
            object? temp = null;
            using (SQLiteConnection connection = new SQLiteConnection(""))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    try
                    {
                        command.CommandText = query;
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    Debug.Write($"{reader[0]} {reader.GetFieldType(0)}|");
                                    return reader[0];
                                }
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SQLite read error: {ex.Message}");
                    }
                }
                connection.Close();
            }
            return temp;
        }

        public object? GetSingleCellData(string query, string[,] paramAndValue)
        {
            object? temp = null;
            try
            {
                if(command != null)
                {
                    command.CommandText = query;
                    for (int i = 0; i < paramAndValue.GetLength(0); i++)
                    {
                        command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                    }
                    command.Prepare();
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                //Debug.Write($"{reader[0]} {reader.GetFieldType(0)}|");
                                return reader[0];
                            }
                        }
                        reader.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite read error: {ex.Message}");
            }
            return temp;
        }

        public void Dispose()
        {
            if (command != null)
            {
                command.Dispose();
            }

            if (connection != null)
            {
                connection.Dispose();
            }
        }
    }
}
