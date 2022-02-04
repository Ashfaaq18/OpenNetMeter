using System.Collections.Concurrent;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels.DataUsageDetailedPagesVM
{
    public class OnlineProfileVM
    {
        public ObservableConcurrentDictionary<string, MyProcess> MyProcesses { get; set; }
        public OnlineProfileVM()
        {
            MyProcesses = new ObservableConcurrentDictionary<string, MyProcess>();
        }
    }
}
