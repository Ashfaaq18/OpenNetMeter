using System.Data.SQLite;
using System.IO;

namespace DatabaseEngine
{
    internal class Connection
    {
        public string ConnectionString { get; set; }
        public Connection(string path, string dbFileName)
        {
            var filePath = Path.Combine(path, dbFileName + ".sqlite");
            ConnectionString = @"Data Source=" + Path.Combine(path, dbFileName + ".sqlite;" + " foreign keys=true");
            if (!System.IO.File.Exists(filePath))
            {
                SQLiteConnection.CreateFile(filePath);
            }
        }
    }
}
