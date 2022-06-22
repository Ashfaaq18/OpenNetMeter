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
            set { selectedProfile = value; OnPropertyChanged("SelectedProfile"); }
        }

        private ObservableCollection<string>? profiles;
        public ObservableCollection<string>? Profiles
        {
            get { return profiles; }
            set
            {
                if (profiles != value)
                {
                    profiles = value;
                    OnPropertyChanged("Profiles");
                }
            }
        }
        public ICommand FilterBtn { get; set; }

        public DataUsageHistoryVM()
        {
            DateStart = DateTime.Today;
            DateEnd = DateTime.Today;
            DateMax = DateTime.Today;
            DateMin = DateTime.Today.AddDays(-60);

            Profiles = new ObservableCollection<string>();
            PropertyChanged += DataUsageHistoryVM_PropertyChanged;

            //set button command
            FilterBtn = new BaseCommand(Filter);
        }

        private void Filter(object obj)
        {
            //show confirmation dialog
            Debug.WriteLine($"Filter {DateStart.ToString("d")} | {DateEnd.ToString("d")}");
            if(SelectedProfile != null)
            {
                using (ApplicationDB dB = new ApplicationDB(SelectedProfile))
                {
                    if (dB.CreateTable() < 0)
                        Debug.WriteLine("Error: Create table");
                    else
                    {
                        List<List<object>> dataStats = dB.GetDataSum_ProcessDateTable(DateStart, DateEnd);
                        for(int i = 0; i< dataStats.Count; i++)
                        {
                            if(dataStats[i].Count == 3)
                                Debug.WriteLine($"processID: {dataStats[i][0]}, dataRecieved: {dataStats[i][1]}, dataSent: {dataStats[i][2]}");
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
            SelectedProfile = Profiles?[0];
        }

        private void DataUsageHistoryVM_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SelectedProfile":
                    break;
                default:
                    break;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
