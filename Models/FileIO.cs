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
        //get all profiles
        public static List<string> GetProfiles()
        {
            string dir = "Profiles";
            string ext = ".WIMD";
            List<string> profiles = new List<string>();
            if (Directory.Exists(dir))
            {
                Debug.WriteLine("exists");
                string[] fileEntries = Directory.GetFiles(dir);
                foreach (string fileName in fileEntries)
                {
                    //Debug.WriteLine(fileName);
                    profiles.Add(fileName.Substring(dir.Length + 1, fileName.Length - dir.Length - ext.Length - 1));
                    //string temp = fileName.Substring(dir.Length + 1, fileName.Length - dir.Length - ext.Length -1);
                   // Debug.WriteLine(temp);
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
                        byte[] Bytes = new byte[8 * 2 + 1 + app.Value.Name.Length];

                        for (int i = 7 * 1; i >= 7 * 0; i--)
                            Bytes[i] = (byte)(app.Value.TotalDataRecv >> 8 * i);

                        for (int i = 7 * 2; i >= 7 * 1; i--)
                            Bytes[i] = (byte)(app.Value.TotalDataSend >> 8 * i);

                        Bytes[16] = (byte)app.Value.Name.Length;

                        for (int i = 0; i < app.Value.Name.Length; i++)
                        {
                            Bytes[17 + i] = (byte)app.Value.Name[i];
                        }
                        stream.Write(Bytes, 0, Bytes.Length);
                    }
                }
            }
            catch (Exception e) { Debug.WriteLine("Cant Write: " + e.Message); }
        }

        public static void ReadFile(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref, string adapterName, bool IsOnlineProfile)
        {
            string path = "Profiles";
            string filename = adapterName + ".WIMD";
            string pathString = Path.Combine(path, filename);
            try
            {
                // Try to create the directory.
                DirectoryInfo di = Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                Debug.WriteLine("The process failed: {0}", e.ToString());
            }

            if (File.Exists(pathString))
            {
                try
                {
                    using (FileStream stream = new FileStream(pathString, FileMode.Open, FileAccess.Read))
                    {
                        (ulong, ulong) data;
                        data = FileIO.ReadFile_MyProcess(dudvm_ref.OnProfVM.MyProcesses, stream);

                        dusvm_ref.TotalDownloadData = data.Item1;
                        dusvm_ref.TotalUploadData = data.Item2;

                        DateTime dateTime = File.GetCreationTime(pathString);
                        dusvm_ref.TotalUsageText = "Total data usage of the past " + (DateTime.Now.DayOfYear - dateTime.DayOfYear).ToString() + " days";
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Cant Read: " + e.Message);
                }
            }
            else
            {
                File.Create(pathString);
                DateTime dateTime = File.GetCreationTime(pathString);
                dusvm_ref.TotalUsageText = "Total data usage of the past " + (DateTime.Now.DayOfYear - dateTime.DayOfYear).ToString() + " days";
            }
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
                Icon ic = null;
                if (process.Length > 0)
                {
                    try { ic = Icon.ExtractAssociatedIcon(process[0].MainModule.FileName); }
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
                //File.SetCreationTime(adapterName + ".WIMD", DateTime.Now);
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
