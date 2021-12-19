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
        public string Image { get; set; }
        private Process[] process;
        public ObservableConcurrentDictionary<string, MyAppInfo> MyApps { get; set; }
        public void EditProcessInfo(double time, string name, ulong data)
        {
            if (name == "")
                name = "system";
            //var watch = Stopwatch.StartNew();
            if (MyApps.TryAdd(name, null))
            {
                process = Process.GetProcessesByName(name);
                if (process.Length > 0 && name != "system")
                {
                    MyApps[name] = new MyAppInfo(name, data, System.Drawing.Icon.ExtractAssociatedIcon(process[0].MainModule.FileName));
                    MyApps[name].Image = Image;
                   // Debug.WriteLine(process[0].MainModule.FileName);
                }
                else if (name == "system")
                {
                    MyApps[name] = new MyAppInfo(name, data, null);
                }
            }
            else
            {
                MyApps[name].DataRecv = MyApps[name].DataRecv + data;
            }
           // watch.Stop();
            //Debug.WriteLine(watch.ElapsedTicks);
            /*implement a task runner in the future to run dictionary addition in the background*/
        }

        public DataUsageDetailedVM()
        {
            MyApps = new ObservableConcurrentDictionary<string, MyAppInfo>();
            Image = "D:\\docs\\time.png";
        }

        
    }
}
