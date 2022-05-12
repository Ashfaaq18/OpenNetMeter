using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
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
        private DispatcherTimer zOrderTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 500), IsEnabled = false };
        private DispatcherTimer relocationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };

        private Window mainWindow;
        public MiniWidgetV(Window mainWindow_ref)
        {
            InitializeComponent();
            DataContext = new MiniWidgetVM();

            relocationTimer.Tick += RelocationTimer_Tick;
            zOrderTimer.Tick += TaskBarStatus_Tick;

            mainWindow = mainWindow_ref;

            if (Properties.Settings.Default.MiniWidgetVisibility)
            {
                this.Visibility = Visibility.Visible;
                zOrderTimer.IsEnabled = true;
            }
            else
            {
                this.Visibility = Visibility.Collapsed;
                zOrderTimer.IsEnabled = false;
            }
        }

        private void FixZorder()
        {
            //const uint GW_CHILD = 5;
            //const uint GW_HWNDNEXT = 2;
            const uint GW_HWNDPREV = 3;
            
            WindowInteropHelper miniWidgetHwnd = new WindowInteropHelper(this);
            IntPtr shellTrayHwnd = NativeMethods.FindWindowByClassName(IntPtr.Zero, "Shell_TrayWnd");
            for (IntPtr h = miniWidgetHwnd.Handle; h != IntPtr.Zero; h = NativeMethods.GetWindow(h, GW_HWNDPREV))
            {
                if(h == shellTrayHwnd)
                {
                    //Debug.WriteLine("this window is behind Shell_TrayWnd");
                    //miniWidgetHwnd.Owner = shellTrayHwnd;
                    NativeMethods.BringWindowToTop(miniWidgetHwnd.Handle);
                    break;
                }
            }
        }

        private void TaskBarStatus_Tick(object sender, EventArgs e)
        {
            if(Properties.Settings.Default.MiniWidgetVisibility)
            {
                FixZorder();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        public void EnableMiniWidgetZorderTimer()
        {
            zOrderTimer.IsEnabled = true;
        }

        private void MenuItem_Hide_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;

            zOrderTimer.IsEnabled = false;

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
