using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Models
{
    public class FileIO
    {
        public static string FolderPath()
        {
            string dir = "OpenNetMeter";
            string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(systemPath, dir);
        }
        //get all profiles
        public static List<string> GetProfiles()
        {
            string ext = ".onm";
            List<string> profiles = new List<string>();
            if (Directory.Exists(FolderPath()))
            {
                string[] fileEntries = Directory.GetFiles(FolderPath());
                foreach (string fileName in fileEntries)
                {
                    profiles.Add(fileName.Substring(FolderPath().Length + 1, fileName.Length - FolderPath().Length - ext.Length - 1));
                }
            }
            return profiles;
        }

        //write all network details of a process to a file
        public static void WriteFile_MyProcess(ObservableConcurrentDictionary<string, MyProcess> apps, string pathString)
        {
            try
            {
                using (FileStream stream = new FileStream(pathString, FileMode.Open, FileAccess.Write))
                {
                    foreach (KeyValuePair<string, MyProcess> app in apps)
                    {
                        /// example of how this section works (for reference)
                        /// test entry:
                        /// app.Value.Name = firefox,                       size: 7
                        /// app.Value.TotalDataRecv = 63.22 KB <= 64737,    size: 8
                        /// app.Value.TotalDataSend = 1.89 KB <= 1931,      size: 8
                        /// app.Value.Name.Length = 7,                      size: 1
                        /// 
                        /// byte[] Byte = new byte[8*2+1+7]                 size: 7+8+8+1 = 24   
                        /// 
                        /// TotalDataRecv           
                        /// ------------------------
                        /// 64737 => ... 0000 0000 1111 1100 1110 0001
                        /// 
                        /// Byte[7] = 64737 >> 8*7 = 0000 0000         
                        /// Byte[6] = 64737 >> 8*6 = 0000 0000          
                        /// Byte[5] = 64737 >> 8*5 = 0000 0000      
                        /// Byte[4] = 64737 >> 8*4 = 0000 0000           
                        /// Byte[3] = 64737 >> 8*3 = 0000 0000
                        /// Byte[2] = 64737 >> 8*2 = 0000 0000
                        /// Byte[1] = 64737 >> 8*1 = 1111 1100
                        /// Byte[0] = 64737 >> 8*0 = 1110 0001
                        /// 
                        /// Byte[0 & 1] = 1111 1100 1110 0001 => 2^15 + 2^14 + 2^13 + 2^12 + 2^11 + 2^10 + 2^7 + 2^6 + 2^5 + 2^0 = 64737
                        /// 64737 / 1024 = 63.22 KB
                        /// 
                        /// Byte[16] = 07
                        /// 
                        /// Byte[17 to 23] = "firefox"
                        /// 

                        byte[] Bytes = new byte[8 * 2 + 1 + app.Value.Name.Length];

                        for (int i = 7 * 1; i >= 7 * 0; i--) // index 7 to 0
                            Bytes[i] = (byte)(app.Value.TotalDataRecv >> 8 * i); //shift bits by 8 to the right

                        for (int i = 7 * 2 + 1; i >= (7 * 1 + 1); i--) // index 15 to 8
                            Bytes[i] = (byte)(app.Value.TotalDataSend >> 8 * i); //shift bits by 8 to the right

                        Bytes[16] = (byte)app.Value.Name.Length;

                        for (int i = 0; i < app.Value.Name.Length; i++) //index from 17 to array max
                        {
                            Bytes[17 + i] = (byte)app.Value.Name[i];
                        }
                        stream.Write(Bytes, 0, Bytes.Length);
                    }
                }
            }
            catch (Exception e) { Debug.WriteLine("Cant Write: " + e.Message); }
        }

        public static void ReadFile(DataUsageSummaryVM dusvm_ref, DataUsageDetailedVM dudvm_ref, string adapterName, bool IsOnlineProfile)
        {
            string filename = adapterName + ".onm";
            string completePath = Path.Combine(FolderPath(), filename);
            
            try
            {
                // Try to create the directory.
                DirectoryInfo di = Directory.CreateDirectory(FolderPath());
            }
            catch (Exception e)
            {
                Debug.WriteLine("The process failed: {0}", e.ToString());
            }

            if (File.Exists(completePath))
            {
                try
                {
                    using (FileStream stream = new FileStream(completePath, FileMode.Open, FileAccess.Read))
                    {
                        (ulong, ulong) data;
                        data = FileIO.ReadFile_MyProcess(dudvm_ref.OnProfVM.MyProcesses, stream);

                        dusvm_ref.TotalDownloadData = data.Item1;
                        dusvm_ref.TotalUploadData = data.Item2;

                        DateTime dateTime = File.GetCreationTime(completePath);
                        int timeDiffInDays = (int)((DateToMins(DateTime.Now) - DateToMins(dateTime))/(60.0 * 24.0));
                        dusvm_ref.TotalUsageText = "Total data usage of the past " + timeDiffInDays.ToString() + " days";
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Cant Read: " + e.Message);
                }
            }
            else
            {
                CreateFile(completePath);
                DateTime dateTime = File.GetCreationTime(completePath);
                int timeDiffInMins = (int)((DateToMins(DateTime.Now) - DateToMins(dateTime)) / (60.0 * 24.0));
                dusvm_ref.TotalUsageText = "Total data usage of the past " + timeDiffInMins.ToString() + " days";
            }
        }

        private static double DateToMins(DateTime t1)
        {
            return t1.Minute + 60 * ( t1.Hour + 24 * ( t1.Day + 30 * ( t1.Month + 12 * ( t1.Year ) ) ) ) ;
        }

        //read the file data into a collection
        public static (ulong,ulong) ReadFile_MyProcess(ObservableConcurrentDictionary<string, MyProcess> apps ,FileStream stream)
        {
            ulong TotalBytesRecv = 0;
            ulong TotalBytesSend = 0;
            while (stream.Position < stream.Length)
            {
                ulong dataRecv = 0;
                ulong dataSend = 0;
                byte[] uploadBytes = new byte[8];
                byte[] downloadBytes = new byte[8];
                for (int j = 0; j < 2; j++) // loop to get upload and download data
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if(j == 0)
                            downloadBytes[i] = (byte)stream.ReadByte();
                        else
                            uploadBytes[i] = (byte)stream.ReadByte();

                    }
                    if(j == 0)
                        dataRecv = BitConverter.ToUInt64(downloadBytes, 0);
                    else
                        dataSend = BitConverter.ToUInt64(uploadBytes, 0);
                }

                int temp = stream.ReadByte();
                byte[] Bytes1 = new byte[temp];
                string tempName = "";
                for (int i = 0; i < temp; i++) // loop to get the name of the app
                {
                    Bytes1[i] = (byte)stream.ReadByte();
                    tempName += (char)Bytes1[i];
                }

                Process[] process = Process.GetProcessesByName(tempName);
                Icon? ic = null;
                if (process.Length > 0)
                {
                    try 
                    {
                        if (process[0].MainModule != null)
                            ic = Icon.ExtractAssociatedIcon(process[0].MainModule!.FileName!);
                        else
                            Debug.WriteLine("process[0].MainModule is null");
                    }
                    catch { Debug.WriteLine("couldnt retrieve icon"); ic = null; }
                }

                apps[tempName] = new MyProcess(tempName, dataRecv, dataSend, ic);
                TotalBytesRecv += dataRecv;
                TotalBytesSend += dataSend;
            }
            return (TotalBytesRecv, TotalBytesSend);
        }

        public static void CreateFile(string pathString)
        {
            try
            {
                var file = File.Create(pathString);
                file.Close();
                File.SetCreationTime(pathString, DateTime.Now);
            }
            catch (Exception ex) { Debug.WriteLine("Cant create: " + ex.Message); }
        }
        public static void DeleteFile(string pathString)
        {
            try
            {
                File.Delete(pathString);
            }
            catch (Exception ex) { Debug.WriteLine("Cant delete: " + ex.Message); }
        }

    }
}
