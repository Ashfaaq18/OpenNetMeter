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

namespace OpenNetMeter.ViewModels
{
    
    public class DataUsageHistoryVM : INotifyPropertyChanged
    {
        private DateTime dateStart;
        public DateTime DateStart
        {
            get { return dateStart; }
            set { dateStart = value; OnPropertyChanged("DateStart"); }
        }
        private DateTime dateEnd;
        public DateTime DateEnd
        {
            get { return dateEnd; }
            set { dateEnd = value; OnPropertyChanged("DateEnd"); }
        }

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

        public DataUsageHistoryVM()
        {
            DateStart = DateTime.Today;
            DateEnd = DateTime.Today;

            Profiles = new ObservableCollection<string>();
            PropertyChanged += DataUsageHistoryVM_PropertyChanged;
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
