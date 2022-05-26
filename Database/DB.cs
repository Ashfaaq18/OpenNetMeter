using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace Database
{
    public enum DataType
    {
        INTEGER,
        TEXT,
        BLOB,
        REAL,
        NUMERIC
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

        public void CreateTable(string tableName, string[] columnNames)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                string allColumnNames = "";
                if (columnNames.Length > 0)
                {
                    allColumnNames += "(";

                    for (int i = 0; i < columnNames.Length - 1; i++)
                    {
                        allColumnNames += columnNames[i] + ", ";
                    }

                    allColumnNames += columnNames[columnNames.Length - 1] + ")";
                }

                connection.Open();
                string sql = "CREATE TABLE IF NOT EXISTS " + tableName + allColumnNames;
                SQLiteCommand cmd = new SQLiteCommand(sql, connection);
                cmd.ExecuteNonQuery();
                connection.Close();
            }
        }

        /// <summary>
        /// CRUD opereations
        /// </summary>
        public void CreateRecord(string tableName, List<KeyValuePair<string,string>> columnAndItsValue)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using(SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    string allColumnNames = "";
                    string allValueNames = "";
                    if(columnAndItsValue.Count > 0)
                    {
                        allColumnNames += "(";
                        allValueNames += "(";

                        for (int i = 0; i < columnAndItsValue.Count-1; i++)
                        {
                            allColumnNames += columnAndItsValue[i].Key + ", ";
                            allValueNames += "@" + columnAndItsValue[i].Key + ", ";
                        }

                        allColumnNames += columnAndItsValue[columnAndItsValue.Count - 1].Key + ")";
                        allValueNames += "@" + columnAndItsValue[columnAndItsValue.Count - 1].Key + ")";

                        try
                        {
                            cmd.CommandText = $"INSERT INTO {tableName}{allColumnNames} VALUES{allValueNames}";

                            Debug.WriteLine(cmd.CommandText);

                            for (int i = 0; i < columnAndItsValue.Count; i++)
                            {
                                cmd.Parameters.AddWithValue($"@{columnAndItsValue[i].Key}", columnAndItsValue[i].Value);
                            }

                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                        }  
                    }     
                }    
                connection.Close();
            }
        }

        public void CreateOrUpdateIfRecordExists(string tableName, List<KeyValuePair<string, string>> columnAndItsValue, int uniqueColIndex)
        {
            if(UpdateRecord(tableName, columnAndItsValue, uniqueColIndex) < 1)
            {
                CreateRecord(tableName, columnAndItsValue);
            }
        }
        public void ReadRecord()
        {

        }
        public int UpdateRecord(string tableName, List<KeyValuePair<string, string>> columnAndItsValue, int uniqueColIndex)
        {
            int rowChangeCount = 0;
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    if (columnAndItsValue.Count > 0)
                    {
                        try
                        {
                            string setString = $"SET ";
                            for (int i = 0; i < columnAndItsValue.Count; i++) //3
                            {
                                setString += $"{columnAndItsValue[i].Key} = @{columnAndItsValue[i].Key}";
                                if ((columnAndItsValue.Count - 1) > i)
                                {
                                    setString += ", ";
                                }

                                cmd.Parameters.AddWithValue($"@{columnAndItsValue[i].Key}", columnAndItsValue[i].Value);
                            }
                            cmd.CommandText = $"UPDATE {tableName} {setString} WHERE {columnAndItsValue[uniqueColIndex].Key} = @{columnAndItsValue[uniqueColIndex].Key}";
                            cmd.Parameters.AddWithValue($"@{columnAndItsValue[uniqueColIndex].Key}", columnAndItsValue[uniqueColIndex].Value);
                            cmd.Prepare();
                            rowChangeCount = cmd.ExecuteNonQuery();
                            //Debug.WriteLine($"Count: {rowChange}");
                            //Debug.WriteLine(cmd.CommandText);
                            //Debug.WriteLine(cmd.Parameters.ToString());
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.ToString());
                            rowChangeCount = -1;
                        }  
                    }
                }
                connection.Close();
                return rowChangeCount;
            }
        }
        public void DeleteRecord(string tableName, KeyValuePair<string, string> name)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(connection))
                {
                    try
                    {
                        cmd.CommandText = $"DELETE FROM {tableName} WHERE {name.Key} = @ItemToDelete";
                        cmd.Parameters.AddWithValue("@ItemToDelete", name.Value);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                    }
                }
                connection.Close();
            }
        }
    }
}
