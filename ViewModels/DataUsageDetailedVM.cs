using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    public class DataUsageDetailedVM
    {
        public ObservableConcurrentDictionary<string, MyAppInfo> MyApps { get; set; }
        public void EditProcessInfo(ref double time, string name, ulong data)
        {
            if (name == null)
                name = "system";

            if (!MyApps.TryAdd(name, new MyAppInfo(name, data)))
                MyApps[name].DataRecv = MyApps[name].DataRecv + data;
        }

        public DataUsageDetailedVM()
        {
            MyApps = new ObservableConcurrentDictionary<string, MyAppInfo>();
        }
    }
}
