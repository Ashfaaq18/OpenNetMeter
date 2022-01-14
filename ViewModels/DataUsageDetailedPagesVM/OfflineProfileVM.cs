using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels.DataUsageDetailedPagesVM
{
    public class OfflineProfileVM
    {
        public ObservableConcurrentDictionary<string, MyAppInfo> MyApps { get; set; }
        public OfflineProfileVM()
        {
            MyApps = new ObservableConcurrentDictionary<string, MyAppInfo>();
        }
    }
}
