using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using OpenNetMeter.Models;
using OpenNetMeter.ViewModels.DataUsageDetailedPagesVM;

namespace OpenNetMeter.ViewModels
{
    public class DataUsageDetailedVM : INotifyPropertyChanged
    {
        public string currentConnection;
        public string CurrentConnection
        {
            get { return currentConnection; }

            set
            {
                if (currentConnection != value)
                {
                    currentConnection = value;
                    OnPropertyChanged("CurrentConnection");
                }
            }
        }

        private ObservableCollection<string>? profiles;
        public ObservableCollection<string>? Profiles
        {
            get{ return profiles; }
            set
            {
                if(profiles != value)
                {
                    profiles = value;
                    OnPropertyChanged("Profiles");
                } 
            }
        }

        private Process[]? process;

        public void GetAppDataInfo(string name, int dataRecv, int dataSend)
        {
            //var watch = Stopwatch.StartNew();
            if (name == null || name == "")
                name = "System";

            if(MyProcesses!= null)
            {
                if (MyProcesses!.TryAdd(name, null))
                {
                    process = Process.GetProcessesByName(name);
                    Icon? ic = null;

                    if (process.Length > 0)
                    {
                        try 
                        {
                            if (process[0].MainModule != null)
                                ic = Icon.ExtractAssociatedIcon(process[0].MainModule!.FileName!);
                            else
                                Debug.WriteLine("process[0].MainModule is null");
                        }
                        catch { Debug.WriteLine("couldnt retrieve icon"); ic = null; }
                    }
                    MyProcesses[name] = new MyProcess(name, (ulong)dataRecv, (ulong)dataSend, ic);
                }
                else
                {
                    MyProcesses[name].TotalDataRecv += (ulong)dataRecv;
                    MyProcesses[name].TotalDataSend += (ulong)dataSend;
                }

                MyProcesses[name].CurrentDataRecv += (ulong)dataRecv;
                MyProcesses[name].CurrentDataSend += (ulong)dataSend;
            }
            
            // watch.Stop();
            //Debug.WriteLine(watch.ElapsedTicks);
            /*implement a task runner in the future to run dictionary addition in the background*/
        }

        public OnlineProfileVM OnProfVM { get; set; }
        public ObservableConcurrentDictionary<string, MyProcess> MyProcesses { get; set; }

        private ConfirmationDialogVM cdvm;

        public DataUsageDetailedVM(ConfirmationDialogVM cdvm_ref)
        {
            cdvm = cdvm_ref;
            cdvm.SetVM(this);
            Profiles = new ObservableCollection<string>();
            MyProcesses = new ObservableConcurrentDictionary<string, MyProcess>();
            //initialize user controls
            OnProfVM = new OnlineProfileVM();
            currentConnection = "";
        }

        //------property changers---------------//

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
