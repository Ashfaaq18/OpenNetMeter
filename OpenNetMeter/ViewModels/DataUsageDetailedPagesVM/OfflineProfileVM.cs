using System.Collections.Concurrent;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels.DataUsageDetailedPagesVM
{
    public class OfflineProfileVM
    {
        public ObservableConcurrentDictionary<string, MyProcess> MyProcesses { get; set; }
        public OfflineProfileVM()
        {
            MyProcesses = new ObservableConcurrentDictionary<string, MyProcess>();
        }
    }
}
