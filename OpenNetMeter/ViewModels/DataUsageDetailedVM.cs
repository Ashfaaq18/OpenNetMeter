using System.Collections.Concurrent;
using OpenNetMeter.Models;

namespace OpenNetMeter.ViewModels
{
    public class DataUsageDetailedVM
    {
        public ObservableConcurrentDictionary<string, MyProcess_Big> MyProcesses { get; set; }

        public DataUsageDetailedVM()
        {
            MyProcesses = new ObservableConcurrentDictionary<string, MyProcess_Big>();
        }
    }
}
