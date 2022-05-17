using System;
using System.Diagnostics;
using System.Windows;
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
        private DispatcherTimer fixZorderTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };
        private DispatcherTimer relocationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };

        private Window mainWindow;
        public MiniWidgetV(Window mainWindow_ref)
        {
            InitializeComponent();
            DataContext = new MiniWidgetVM();

            fixZorderTimer.Tick += FixZorderTimer_Tick;
            relocationTimer.Tick += RelocationTimer_Tick;

            mainWindow = mainWindow_ref;

            this.Visibility = Visibility.Visible;
            fixZorderTimer.IsEnabled = true;

            Loaded += MiniWidgetV_Loaded;
        }

        private void MiniWidgetV_Loaded(object sender, RoutedEventArgs e)
        {
            if (!Properties.Settings.Default.MiniWidgetVisibility)
            {
                this.Visibility = Visibility.Hidden;
                fixZorderTimer.IsEnabled = false;
            }
        }

        private void FixZorderTimer_Tick(object sender, EventArgs e)
        {
            const int HWND_TOPMOST = -1;
            const int HWND_NOTOPMOST = -2;
            const string SHELLTRAY = "Shell_traywnd";

            WindowInteropHelper thisWin = new WindowInteropHelper(this);
            IntPtr shellTray = NativeMethods.GetWindowByClassName(IntPtr.Zero ,SHELLTRAY);

            //Reassign owner when explorer.exe restarts
            if (thisWin.Owner != shellTray)
            {
                Debug.WriteLine("set owner again");
                thisWin.Owner = shellTray;
            }

            //check if window is behind the taskbar. If yes, bring it in front without activating it.
            for (IntPtr h = thisWin.Handle; h != IntPtr.Zero; h = NativeMethods.GetWindow(h, (uint)NativeMethods.GW.HWNDPREV))
            {
                if (h == shellTray)
                {
                    Debug.WriteLine("this window is behind Shell_TrayWnd");
                    NativeMethods.SetWindowPos(thisWin.Handle, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0, 
                        (int)NativeMethods.SWP.ASYNCWINDOWPOS |
                        (int)NativeMethods.SWP.NOACTIVATE |
                        (int)NativeMethods.SWP.NOMOVE |
                        (int)NativeMethods.SWP.NOSIZE);
                    //NativeMethods.SetWindowPos(thisWin.Handle, (IntPtr)HWND_NOTOPMOST, 0, 0, 0, 0, SWP_ASYNCWINDOWPOS | SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
                    break;
                }
            }
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MenuItem_Hide_Click(object sender, RoutedEventArgs e)
        {
            HideMiniWidget();
        }
        private void MenuItem_Open_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow != null)
            {
                mainWindow.Visibility = Visibility.Visible;
                mainWindow.Activate();
            }
        }

        /// <summary>
        /// Save window position when user moves window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RelocationTimer_Tick(object sender, EventArgs e)
        {
            relocationTimer.IsEnabled = false;
            //Do end of relocation processing
            SaveWinPos((int)this.Left, (int)this.Top);
        }

        public void SaveWinPos(int x, int y)
        {
            Properties.Settings.Default.MiniWidgetPos = new System.Drawing.Point(x, y);
            Properties.Settings.Default.Save();
        }

        private void Window_LocationChanged(object sender, System.EventArgs e)
        {
            relocationTimer.IsEnabled = true;
            relocationTimer.Stop();
            relocationTimer.Start();
        }

        public void ShowMiniWidget()
        {
            this.Visibility = Visibility.Visible;
            this.Activate();
            fixZorderTimer.IsEnabled = true;

            Properties.Settings.Default.MiniWidgetVisibility = true;
            Properties.Settings.Default.Save();
        }

        public void HideMiniWidget()
        {
            this.Visibility = Visibility.Hidden;
            fixZorderTimer.IsEnabled = false;

            Properties.Settings.Default.MiniWidgetVisibility = false;
            Properties.Settings.Default.Save();
        }
    }
}
