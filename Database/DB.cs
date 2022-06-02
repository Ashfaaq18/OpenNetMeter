using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace Database
{
    public static class DataType
    {
        public static string INTEGER = "INTEGER";
        public static string TEXT = "TEXT";
        public static string BLOB = "BLOB";
        public static string REAL = "REAL";
        public static string NUMERIC = "NUMERIC";
    }

    public class DB
    {
        
        private string connectionString;
        public DB(string path, string dbFileName)
        {
            var filePath = Path.Combine(path, dbFileName + ".sqlite");
            connectionString = @"Data Source=" + Path.Combine(path, dbFileName + ".sqlite");
            if (!System.IO.File.Exists(filePath))
            {
                SQLiteConnection.CreateFile(filePath);
            }
        }

        public int GetTableColumnCount(string tableName)
        {
            int result = 0;
            using(SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = $"SELECT * from {tableName}";
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            result = reader.FieldCount;
                            reader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ReadAllRecords error: {ex.Message}");
                    }
                }
                connection.Close();
            }
            return result;
        }

        public bool TableExists(string tableName)
        {
            bool result = false;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name=@name";
                        cmd.Parameters.AddWithValue("@name", tableName);
                        object getTableName = cmd.ExecuteScalar();
                        if(getTableName != null)
                        {
                            if (getTableName.ToString() == tableName)
                                result = true;
                        }
                        
                    }
                    catch(Exception ex)
                    {
                        Debug.WriteLine($"TableExists error: {ex.Message}");
                    }
                }
                connection.Close();
            }
            return result;
        }

        public void CreateTable(string tableName, string[] columnNames)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using(SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        string allColumnNames = "";
                        if (columnNames.Length > 0)
                        {
                            allColumnNames += "(";
                            for (int i = 0; i < columnNames.Length - 1; i++)
                            {
                                allColumnNames += $"{columnNames[i]} , ";
                            }
                        }
                        allColumnNames += $"{columnNames[columnNames.Length - 1]})";
                        cmd.CommandText = $"CREATE TABLE {tableName}{allColumnNames}";
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"CreateTable error: {ex.Message}");
                    }
                }
                connection.Close();
            }
        }

        /// <summary>
        /// CRUD operations
        /// </summary>
        public void CreateRecord(string tableName, string[,] columnAndItsValue)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using(SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        string allColumnNames = "";
                        string allValueNames = "";
                        if (columnAndItsValue.GetLength(0) > 0)
                        {
                            allColumnNames += "(";
                            allValueNames += "(";

                            for (int i = 0; i < columnAndItsValue.GetLength(0) - 1; i++)
                            {
                                allColumnNames += columnAndItsValue[i, 0] + ", ";
                                allValueNames += "@" + columnAndItsValue[i, 0] + ", ";
                            }

                            allColumnNames += columnAndItsValue[columnAndItsValue.GetLength(0) - 1, 0] + ")";
                            allValueNames += "@" + columnAndItsValue[columnAndItsValue.GetLength(0) - 1, 0] + ")";

                            cmd.CommandText = $"INSERT INTO {tableName}{allColumnNames} VALUES{allValueNames}";

                            Debug.WriteLine(cmd.CommandText);

                            for (int i = 0; i < columnAndItsValue.GetLength(0); i++)
                            {
                                cmd.Parameters.AddWithValue($"@{columnAndItsValue[i, 0]}", columnAndItsValue[i, 1]);
                            }

                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"CreateRecord error: {ex.Message}");
                    }  
                }    
                connection.Close();
            }
        }

        public void CreateOrUpdateIfRecordExists(string tableName, string[,] columnAndItsValue, int uniqueColIndex)
        {
            if(UpdateRecord(tableName, columnAndItsValue, uniqueColIndex) < 1)
            {
                CreateRecord(tableName, columnAndItsValue);
            }
        }
        public void ReadAllRecords(string tableName)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = $"SELECT * from {tableName}";
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                int colCount = reader.FieldCount;
                                while (reader.Read())
                                {
                                    for (int i = 0; i < colCount; i++)
                                    {
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
                        Debug.WriteLine($"ReadAllRecords error: {ex.Message}");
                    }
                }
                connection.Close();
            }
        }
        public int UpdateRecord(string tableName, string[,] columnAndItsValue, int uniqueColIndex)
        {
            int rowChangeCount = 0;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        if (columnAndItsValue.GetLength(0) > 0)
                        {
                            string setString = $"SET ";
                            for (int i = 0; i < columnAndItsValue.GetLength(0); i++) //3
                            {
                                setString += $"{columnAndItsValue[i, 0]} = @{columnAndItsValue[i, 0]}";
                                if ((columnAndItsValue.GetLength(0) - 1) > i)
                                {
                                    setString += ", ";
                                }

                                cmd.Parameters.AddWithValue($"@{columnAndItsValue[i, 0]}", columnAndItsValue[i, 1]);
                            }
                            cmd.CommandText = $"UPDATE {tableName} {setString} WHERE {columnAndItsValue[uniqueColIndex, 0]} = @{columnAndItsValue[uniqueColIndex, 0]}";
                            cmd.Parameters.AddWithValue($"@{columnAndItsValue[uniqueColIndex, 0]}", columnAndItsValue[uniqueColIndex, 1]);
                            cmd.Prepare();
                            rowChangeCount = cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateRecord error: {ex.Message}");
                        rowChangeCount = -1;
                    }
                }
                connection.Close();
                return rowChangeCount;
            }
        }
        public void DeleteRecord(string tableName, (string, string) name)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = $"DELETE FROM {tableName} WHERE {name.Item1} = @ItemToDelete";
                        cmd.Parameters.AddWithValue("@ItemToDelete", name.Item2);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"DeleteRecord error: {ex.Message}");
                    }
                }
                connection.Close();
            }
        }
    }
}
