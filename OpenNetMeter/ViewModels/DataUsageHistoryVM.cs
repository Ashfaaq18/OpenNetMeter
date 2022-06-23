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
                selectedProfile = value;
                OnPropertyChanged("SelectedProfile");
            }
        }

        public ObservableCollection<string>? Profiles { get; set; }
        public ObservableCollection<MyProcess> MyProcesses { get; set; }
        public ICommand FilterBtn { get; set; }

        private FileSystemWatcher watcher;
        public DataUsageHistoryVM()
        {
            DateStart = DateTime.Today;
            DateEnd = DateTime.Today;
            DateMax = DateTime.Today;
            DateMin = DateTime.Today.AddDays(-60);

            Profiles = new ObservableCollection<string>();
            MyProcesses = new ObservableCollection<MyProcess>();
            watcher = new FileSystemWatcher(ApplicationDB.GetFilePath(), "*.sqlite");
            watcher.Created += Watcher_Created;
            watcher.Deleted += Watcher_Deleted;
            watcher.EnableRaisingEvents = true;

            //set button command
            FilterBtn = new BaseCommand(Filter);
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                GetAllDBFiles();
            });
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                GetAllDBFiles();
            });
        }

        private void Filter(object obj)
        {
            MyProcesses.Clear();
            //show confirmation dialog
            Debug.WriteLine($"Filter {DateStart.ToString("d")} | {DateEnd.ToString("d")}");
            if(SelectedProfile != null)
            {
                using (ApplicationDB dB = new ApplicationDB(SelectedProfile))
                {
                    List<List<object>> dataStats = dB.GetDataSum_ProcessDateTable(DateStart, DateEnd);
                    for(int i = 0; i< dataStats.Count; i++)
                    {
                        if(dataStats[i].Count == 3)
                        {
                            if((string)dataStats[i][0] != "")
                                MyProcesses.Add(new MyProcess((string)dataStats[i][0], (long)dataStats[i][1], (long)dataStats[i][2], null));
                            else
                                MyProcesses.Add(new MyProcess("System", (long)dataStats[i][1], (long)dataStats[i][2], null));

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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));

        public void Dispose()
        {
            watcher.Dispose();
        }
    }
}
