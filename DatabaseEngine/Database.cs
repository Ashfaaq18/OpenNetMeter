using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace DatabaseEngine
{
    public class Database
    {
        
        private string connectionString;
        public Database(string path, string dbFileName)
        {
            var filePath = Path.Combine(path, dbFileName + ".sqlite");
            connectionString = @"Data Source=" + Path.Combine(path, dbFileName + ".sqlite;" + " foreign keys=true;");  
            if (!System.IO.File.Exists(filePath))
            {
                SQLiteConnection.CreateFile(filePath);
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
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        Debug.WriteLine(query);
                        cmd.CommandText = query;
                        rowChangeCount = cmd.ExecuteNonQuery();
                    }
                    catch(Exception ex)
                    {
                        Debug.WriteLine($"SQLite Error: {ex.Message}");
                        rowChangeCount = -1;
                    }
                }
                connection.Close();
            }
            return rowChangeCount;
        }

        public int RunSQLiteNonQuery(string query, string[,] paramAndValue)
        {
            int rowChangeCount = 0;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        Debug.WriteLine(query);
                        cmd.CommandText = query;
                        for(int i = 0; i<paramAndValue.GetLength(0); i++)
                        {
                            cmd.Parameters.AddWithValue(paramAndValue[i,0], paramAndValue[i,1]);
                        }
                        cmd.Prepare();
                        rowChangeCount = cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SQLite Error: {ex.Message}");
                        rowChangeCount = -1;
                    }
                }
                connection.Close();
            }
            return rowChangeCount;
        }

        public int RunSQLiteNonQueryTransaction(string query, string[] columns, List<string[]> values)
        {
            int rowChangeCount = 0;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        using (SQLiteTransaction transaction = connection.BeginTransaction())
                        {
                            if (columns.GetLength(0) == values[0].GetLength(0))
                            {
                                for (int i = 0; i < values.Count; i++)
                                {
                                    cmd.CommandText = query;
                                    for (int j = 0; j < columns.GetLength(0); j++)
                                    {
                                        cmd.Parameters.AddWithValue($"@{columns[j]}", values[i][j]);
                                        //Debug.Write(values[i][j] + ",");
                                    }
                                    //Debug.WriteLine("");
                                    cmd.Prepare();
                                    rowChangeCount += cmd.ExecuteNonQuery();
                                }
                                transaction.Commit();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SQLite Error: {ex.Message}");
                        rowChangeCount = -1;
                    }
                    
                }
                connection.Close();
            }
            return rowChangeCount;
        }

        public List<List<object>> GetMultipleCellData(string query)
        {
            List<List<object>> temp = new List<List<object>>();
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = query;
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
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
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = query;
                        for (int i = 0; i < paramAndValue.GetLength(0); i++)
                        {
                            cmd.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                        }
                        cmd.Prepare();
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
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
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = query;
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
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
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = query;
                        for (int i = 0; i < paramAndValue.GetLength(0); i++)
                        {
                            cmd.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                        }
                        cmd.Prepare();
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
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


    }
}
