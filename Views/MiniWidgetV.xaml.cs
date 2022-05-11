using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using OpenNetMeter.Models;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for MiniWidgetV.xaml
    /// </summary>
    public partial class MiniWidgetV : Window
    {
        private DispatcherTimer taskBarStatus = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };
        private DispatcherTimer relocationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };

        private Window mainWindow;
        public MiniWidgetV(Window mainWindow_ref)
        {
            InitializeComponent();
            DataContext = new MiniWidgetVM();

            this.Left = Properties.Settings.Default.MiniWidgetPos.X;
            this.Top = Properties.Settings.Default.MiniWidgetPos.Y;

            this.Visibility = Properties.Settings.Default.MiniWidgetVisibility ? Visibility.Visible : Visibility.Collapsed;

            relocationTimer.Tick += RelocationTimer_Tick;

            taskBarStatus.Tick += TaskBarStatus_Tick;
            taskBarStatus.IsEnabled = false;

            mainWindow = mainWindow_ref;
        }

        private void TaskBarStatus_Tick(object sender, EventArgs e)
        {
            //IntPtr hWnd = NativeMethods.FindWindowByClassName(IntPtr.Zero, "Shell_TrayWnd");
            //if (hWnd != IntPtr.Zero)
            //{
            //    //IsTaskBarVisible = IsWindowVisible(hWnd);
            //    if(!NativeMethods.IsWindowVisible(hWnd))
            //        Debug.WriteLine("taskbar hidden");
            //}
            //Debug.WriteLine("Diff: " + Math.Abs(SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Height));
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MenuItem_Hide_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;

            Properties.Settings.Default.MiniWidgetVisibility = false;
            Properties.Settings.Default.Save();
        }
        private void RelocationTimer_Tick(object sender, EventArgs e)
        {
            relocationTimer.IsEnabled = false;

            //Do end of relocation processing
            Properties.Settings.Default.MiniWidgetPos = new System.Drawing.Point((int)this.Left, (int)this.Top);
            Properties.Settings.Default.Save();
        }

        private void Window_LocationChanged(object sender, System.EventArgs e)
        {
            relocationTimer.IsEnabled = true;
            relocationTimer.Stop();
            relocationTimer.Start();
        }

        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow != null)
            {
                mainWindow.Visibility = Visibility.Visible;
                mainWindow.Activate();
            }
        }

    }
}
