using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;

namespace DatabaseEngine
{
    public class Database : IDisposable
    {
        private SQLiteConnection? connection;
        private bool disposed = false;

        /// <summary>
        /// Creates and opens a database connection.
        /// </summary>
        /// <param name="path">Directory path for the database file</param>
        /// <param name="dbFileName">Database file name (without extension)</param>
        /// <param name="extraParams">Additional connection string parameters</param>
        public Database(string path, string dbFileName, string[]? extraParams = null)
        {
            string connectionString = new Connection(path, dbFileName).ConnectionString;

            // Append any extra parameters to connection string
            if (extraParams != null)
            {
                foreach (var param in extraParams)
                {
                    connectionString += $";{param}";
                }
            }

            connection = new SQLiteConnection(connectionString);
            connection.Open();
        }

        /// <summary>
        /// Configures the database for optimal concurrent access.
        /// Call once after opening the connection.
        /// </summary>
        public void ConfigureForConcurrency()
        {
            // WAL mode allows concurrent reads during writes
            RunSQLiteNonQuery("PRAGMA journal_mode=WAL;");

            // Wait up to 5 seconds if database is locked instead of failing immediately
            RunSQLiteNonQuery("PRAGMA busy_timeout=5000;");

            // Good balance of durability and performance with WAL
            RunSQLiteNonQuery("PRAGMA synchronous=NORMAL;");
        }

        /// <summary>
        /// Executes a non-query SQL command (INSERT, UPDATE, DELETE, CREATE, etc.)
        /// </summary>
        /// <param name="query">SQL query string</param>
        /// <returns>Number of rows affected, or negative value on error</returns>
        public int RunSQLiteNonQuery(string query)
        {
            if (connection == null) return -2;

            try
            {
                using var command = new SQLiteCommand(query, connection);
                return command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite Error: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Executes a non-query SQL command with parameters.
        /// </summary>
        /// <param name="query">SQL query string with parameter placeholders</param>
        /// <param name="paramAndValue">2D array of parameter names and values</param>
        /// <returns>Number of rows affected, or negative value on error</returns>
        public int RunSQLiteNonQuery(string query, string[,] paramAndValue)
        {
            if (connection == null) return -2;

            try
            {
                using var command = new SQLiteCommand(query, connection);

                for (int i = 0; i < paramAndValue.GetLength(0); i++)
                {
                    command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                }

                command.Prepare();
                return command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite Error: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Executes multiple non-query commands within a single transaction.
        /// More efficient for batch operations.
        /// </summary>
        /// <param name="action">Action that performs the database operations</param>
        /// <returns>True if transaction committed successfully</returns>
        public bool RunInTransaction(Action<Database> action)
        {
            if (connection == null) return false;

            using var transaction = connection.BeginTransaction();
            try
            {
                action(this);
                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite Transaction Error: {ex.Message}");
                transaction.Rollback();
                return false;
            }
        }

        /// <summary>
        /// Executes a query and returns all rows/columns as a list of lists.
        /// </summary>
        public List<List<object>> GetMultipleCellData(string query)
        {
            var result = new List<List<object>>();

            if (connection == null) return result;

            try
            {
                using var command = new SQLiteCommand(query, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var row = new List<object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row.Add(reader[i]);
                    }
                    result.Add(row);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite read error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Executes a parameterized query and returns all rows/columns as a list of lists.
        /// </summary>
        public List<List<object>> GetMultipleCellData(string query, string[,] paramAndValue)
        {
            var result = new List<List<object>>();

            if (connection == null) return result;

            try
            {
                using var command = new SQLiteCommand(query, connection);

                for (int i = 0; i < paramAndValue.GetLength(0); i++)
                {
                    command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                }

                command.Prepare();

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = new List<object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row.Add(reader[i]);
                    }
                    result.Add(row);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite read error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Executes a parameterized query and returns the first cell of the first row.
        /// </summary>
        public object? GetSingleCellData(string query, string[,] paramAndValue)
        {
            if (connection == null) return null;

            try
            {
                using var command = new SQLiteCommand(query, connection);

                for (int i = 0; i < paramAndValue.GetLength(0); i++)
                {
                    command.Parameters.AddWithValue(paramAndValue[i, 0], paramAndValue[i, 1]);
                }

                command.Prepare();
                return command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SQLite read error: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            connection?.Dispose();
            connection = null;
        }
    }
}