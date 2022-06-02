using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OpenNetMeter.Models
{
    internal class ApplicationDB
    {
        private string filePath;
        private Database.DB dB;
        public ApplicationDB()
        {
            //set DB file path
            string? appName = Assembly.GetEntryAssembly()?.GetName().Name;
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (appName != null)
                filePath = Path.Combine(path, appName);
            else
                filePath = Path.Combine(path, "OpenNetMeter");

            //create or update table
            string tableName = "phones";
            string[] columns = new string[] {
                    $"brand {Database.DataType.TEXT}",
                    $"model {Database.DataType.TEXT}",
                    $"description {Database.DataType.TEXT}",
                    $"modelNo {Database.DataType.INTEGER}"
                };
            dB = new(filePath, "test");

            if (!dB.TableExists(tableName))
            {
                dB.CreateTable(tableName, columns);
            }
            else
            {
                int temp = dB.GetTableColumnCount(tableName);
                if (columns.Length == temp)
                {
                   
                }
                else
                {
                    if(columns.Length < temp)
                    {

                    }
                    else
                    {

                    }
                }
            }

            dB.CreateOrUpdateIfRecordExists("phones",
            new string[,]{
                { "brand", "samsung"},
                { "model", "j3"},
                { "description", "this is a desc samsungs"}
            }, 0);

            dB.CreateOrUpdateIfRecordExists("phones",
            new string[,]{
                { "brand", "xiaomi"},
                { "model", "Mi 3"},
                { "description", "this is a desc Xiaomi"}
            }, 0);

            dB.CreateOrUpdateIfRecordExists("phones",
            new string[,]{
                { "brand", "test"},
                { "model", "Mi 12"},
                { "description", "this is a desc test"}
            }, 0);

            dB.ReadAllRecords("phones");
        }

        public void Update(string[,] record)
        {
            if(dB != null)
                dB.CreateOrUpdateIfRecordExists("phones", record, 0);
        }

        public void Remove()
        {

        }
    }
}
