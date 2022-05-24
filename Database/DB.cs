using System.Data.SQLite;
using System.IO;

namespace Database
{
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

        public void CreateTable(string tableName, string columnNames)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string sql = "CREATE TABLE IF NOT EXISTS " + tableName + columnNames; ;
                SQLiteCommand cmd = new SQLiteCommand(sql, connection);
                cmd.ExecuteNonQuery();
                connection.Close();
            }
        }
        
        /// <summary>
        /// CRUD opereations
        /// </summary>
        public void CreateRecord()
        {

        }
        public void ReadRecord()
        {

        }
        public void UpdateRecord()
        {

        }
        public void DeleteRecord()
        {

        }
    }
}
