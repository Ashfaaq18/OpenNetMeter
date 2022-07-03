using OpenNetMeter.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OpenNetMeter.ViewModels
{
    
    public class DataUsageHistoryVM : IDisposable, INotifyPropertyChanged
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
        public ObservableCollection<MyProcess> MyProcesses { get; set; }

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

        private FileSystemWatcher watcher;
        public DataUsageHistoryVM()
        {
            DateStart = DateTime.Today;
            DateEnd = DateTime.Today;
            DateMax = DateTime.Today;
            DateMin = DateTime.Today.AddDays(-1 * ApplicationDB.DataStoragePeriodInDays);
            TotalDownloadData = 0;
            TotalUploadData = 0;

            PropertyChanged += DataUsageHistoryVM_PropertyChanged;

            Profiles = new ObservableCollection<string>();
            MyProcesses = new ObservableCollection<MyProcess>();
            watcher = new FileSystemWatcher(ApplicationDB.GetFilePath(), "*.sqlite");
            watcher.Created += OnFile_Created;
            watcher.Deleted += OnFile_Deleted;
            watcher.EnableRaisingEvents = true;

            //set button command
            FilterBtn = new BaseCommand(Filter, true);
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

        private void OnFile_Created(object sender, FileSystemEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                GetAllDBFiles();
            });
        }

        private void OnFile_Deleted(object sender, FileSystemEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                GetAllDBFiles();
            });
        }

        private void Filter(object? obj)
        {
            MyProcesses.Clear();
            TotalDownloadData = 0;
            TotalUploadData = 0;
            //show confirmation dialog
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
                                if ((string)dataStats[i][0] != "")
                                    MyProcesses.Add(new MyProcess((string)dataStats[i][0], (long)dataStats[i][1], (long)dataStats[i][2], null));
                                else
                                    MyProcesses.Add(new MyProcess("System", (long)dataStats[i][1], (long)dataStats[i][2], null));

                                TotalDownloadData += (long)dataStats[i][1];
                                TotalUploadData += (long)dataStats[i][2];
                            }
                            //Debug.WriteLine($"processID: {dataStats[i][0]}, dataRecieved: {dataStats[i][1]}, dataSent: {dataStats[i][2]}");
                        }
                    }
                }
            }
        }

        public void GetAllDBFiles()
        {
            string[] fileArray = Directory.GetFiles(ApplicationDB.GetFilePath(), "*.sqlite");
            Profiles?.Clear();
            for(int i = 0; i<fileArray.Length; i++)
            {
                Profiles?.Add(Path.GetFileNameWithoutExtension(fileArray[i]));
                Debug.WriteLine(Path.GetFileNameWithoutExtension(fileArray[i]));
            }
            if (Profiles?.Count > 0)
                SelectedProfile = Profiles?[0];
        }

        public void DeleteAllDBFiles()
        {
            DirectoryInfo? dir = new DirectoryInfo(ApplicationDB.GetFilePath());
            foreach (FileInfo? file in dir.GetFiles("*.sqlite"))
            {
                try
                {
                    file.Delete();
                }
                catch (IOException ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public void Dispose()
        {
            watcher.Dispose();
        }
    }
}
