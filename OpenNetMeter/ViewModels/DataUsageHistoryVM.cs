using OpenNetMeter.Models;
using OpenNetMeter.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace OpenNetMeter.ViewModels
{
    
    public class DataUsageHistoryVM : INotifyPropertyChanged
    {
        public DateTime DateMax { get; private set; }
        public DateTime DateMin { get; private set; }
        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }

        private string? selectedProfile;
        public string? SelectedProfile 
        {
            get { return selectedProfile; }
            set
            {
                if(value != selectedProfile)
                {
                    selectedProfile = value;
                    OnPropertyChanged("SelectedProfile");
                }
            }
        }

        public ObservableCollection<string>? Profiles { get; set; }
        public ObservableCollection<MyProcess_Small> MyProcesses { get; set; }

        private long totalDownloadData;
        public long TotalDownloadData 
        {
            get { return totalDownloadData; }
            set
            {
                if (value != totalDownloadData)
                {
                    totalDownloadData = value;
                    OnPropertyChanged("TotalDownloadData");
                }
            }
        }

        private long totalUploadData;
        public long TotalUploadData
        {
            get { return totalUploadData; }
            set
            {
                if (value != totalUploadData)
                {
                    totalUploadData = value;
                    OnPropertyChanged("TotalUploadData");
                }
            }
        }

        public ICommand FilterBtn { get; set; }
        public DataUsageHistoryVM()
        {
            UpdateDates();
            TotalDownloadData = 0;
            TotalUploadData = 0;

            PropertyChanged += DataUsageHistoryVM_PropertyChanged;

            Profiles = new ObservableCollection<string>();
            MyProcesses = new ObservableCollection<MyProcess_Small>();;

            //set button command
            FilterBtn = new BaseCommand(Filter, true);

            // initial load
            GetAllDBFiles();
        }

        public void UpdateDates()
        {
            DateStart = DateTime.Today;
            DateEnd = DateTime.Today;
            DateMax = DateTime.Today;
            DateMin = DateTime.Today.AddDays(-1 * ApplicationDB.DataStoragePeriodInDays);
        }

        private void DataUsageHistoryVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SelectedProfile":
                    MyProcesses.Clear();
                    TotalDownloadData = 0;
                    TotalUploadData = 0;
                    break;
                default:
                    break;
            }
        }

        private void Filter(object? obj)
        {
            MyProcesses.Clear();
            TotalDownloadData = 0;
            TotalUploadData = 0;
            Debug.WriteLine($"Filter {DateStart.ToString("d")} | {DateEnd.ToString("d")}");
            if(SelectedProfile != null)
            {
                using (ApplicationDB dB = new ApplicationDB(SelectedProfile, new string[] { "Read Only=True"}))
                {
                    List<List<object>> dataStats = dB.GetDataSum_ProcessDateTable(DateStart, DateEnd);
                    for(int i = 0; i< dataStats.Count; i++)
                    {
                        if(dataStats[i].Count == 3)
                        {
                            if(!Convert.IsDBNull(dataStats[i][0]) && !Convert.IsDBNull(dataStats[i][1]) && !Convert.IsDBNull(dataStats[i][2]))
                            {
                                string processName = Convert.ToString(dataStats[i][0])!;
                                MyProcesses.Add(new MyProcess_Small(processName, Convert.ToInt64(dataStats[i][1]), Convert.ToInt64(dataStats[i][2]), ProcessIconCache.GetIcon(processName)));

                                TotalDownloadData += Convert.ToInt64(dataStats[i][1]);
                                TotalUploadData += Convert.ToInt64(dataStats[i][2]);
                            }
                            //Debug.WriteLine($"processID: {dataStats[i][0]}, dataRecieved: {dataStats[i][1]}, dataSent: {dataStats[i][2]}");
                        }
                    }
                }
            }
        }

        public void GetAllDBFiles()
        {
            // Ensure collection updates occur on the UI thread
            if (!App.Current.Dispatcher.CheckAccess())
            {
                App.Current.Dispatcher.Invoke(() => GetAllDBFiles());
                return;
            }

            Profiles?.Clear();

            // Ensure DB exists and read adapters from it
            using (ApplicationDB dB = new ApplicationDB(string.Empty))
            {
                dB.CreateTable();
                var adapters = dB.GetAllAdapters();
                foreach (var a in adapters)
                {
                    Profiles?.Add(a);
                }
            }

            if (Profiles?.Count > 0)
                SelectedProfile = Profiles?[0];
        }

        public void DeleteAllDBFiles()
        {
            try
            {
                string path = ApplicationDB.GetUnifiedDBFullPath();
                if (File.Exists(path))
                    File.Delete(path);
                Profiles?.Clear();
                SelectedProfile = null;
            }
            catch (IOException ex)
            {
                EventLogger.Error(ex.Message);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

    }
}
