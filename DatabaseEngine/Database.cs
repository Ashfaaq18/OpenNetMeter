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
            connectionString = @"Data Source=" + Path.Combine(path, dbFileName + ".sqlite");  
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

        public List<List<object>> RunSQLiteReader(string query)
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
        
        public List<List<object>> RunSQLiteReader(string query, string[,] paramAndValue)
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
    }
}
