using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    public class DataUsageDetailedVM
    {
        private Process[] process;
        public ObservableConcurrentDictionary<string, MyAppInfo> MyApps { get; set; }
        public void GetAppDataInfo(string name, int dataRecv, int dataSend)
        {
            //var watch = Stopwatch.StartNew();
            if (name == null || name == "")
                name = "unknown1";
            if (MyApps.TryAdd(name, null))
            {
                process = Process.GetProcessesByName(name);
                Icon ic = null;
                
                if (process.Length > 0)
                {
                    try { ic = System.Drawing.Icon.ExtractAssociatedIcon(process[0].MainModule.FileName); }
                    catch { Debug.WriteLine("couldnt retrieve icon"); ic = null; }

                    MyApps[name] = new MyAppInfo(name, dataRecv, dataSend, ic);
                }
            }
            else
            {
                MyApps[name].DataRecv = MyApps[name].DataRecv + (ulong)dataRecv;
                MyApps[name].DataSend = MyApps[name].DataSend + (ulong)dataSend;
            }

           // watch.Stop();
            //Debug.WriteLine(watch.ElapsedTicks);
            /*implement a task runner in the future to run dictionary addition in the background*/
        }


        public DataUsageDetailedVM()
        {
            MyApps = new ObservableConcurrentDictionary<string, MyAppInfo>();
        }

        
    }
}
