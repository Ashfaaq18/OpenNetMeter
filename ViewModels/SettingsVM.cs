using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels
{
    public class SettingsVM
    {
        public ICommand ResetBtn { get; set; }
        private DataUsageSummaryVM dusvm;
        private DataUsageDetailedVM dudvm;
        private NetworkInfo netInfo;
        public SettingsVM(ref DataUsageSummaryVM dusvm_ref, ref DataUsageDetailedVM dudvm_ref, ref NetworkInfo netInfo_ref)
        {
            dusvm = dusvm_ref;
            dudvm = dudvm_ref;
            netInfo = netInfo_ref;
            ResetBtn = new BaseCommand(ResetTotalData);
        }

        private void ResetTotalData(object obj)
        {
            dusvm.CurrentSessionDownloadData = 0;
            dusvm.CurrentSessionUploadData = 0;
            dusvm.TotalDownloadData = 0;
            dusvm.TotalUploadData = 0;
            foreach (var row in dudvm.MyApps.ToList())
            {
                dudvm.MyApps.Remove(row.Key);
            }

            netInfo.ResetWriteFileAndSpeed();
        }
    }
}
