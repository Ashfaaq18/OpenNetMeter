using System.Windows;
using OpenNetMeter.ViewModels;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using System;
using System.Timers;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private bool isSingleInstance = false;
        private Mutex mutex;
        public bool IsSingleInstance()
        {
            bool createdNew;
            mutex = new Mutex(true, "{6C4919CA-062E-47E3-85CC-2393D00CBA4A}", out createdNew);

            if (!createdNew)
            {
                //exit app
                Application.Current.Shutdown();
                return false;
            }
            else
            {
                return true;
            }
        }

        private AboutWindow aboutWin;
        private TrayPopupWinV trayWin;
        private Forms.NotifyIcon ni;
        private Forms.ContextMenuStrip cm;
        private bool balloonShow;
        private bool forceHideTrayWin;
        private System.Drawing.Point p;
        public MainWindow()
        {
            isSingleInstance = IsSingleInstance();

            if (isSingleInstance)
            {
                InitializeComponent();

                trayWin = new TrayPopupWinV();
                DataContext = new NavigationAndTasksVM((TrayPopupVM)trayWin.DataContext);
                aboutWin = new AboutWindow();

                //initialize system tray
                trayWin.Topmost = true;
                trayWin.Visibility = Visibility.Hidden;
                ni = new Forms.NotifyIcon();
                cm = new Forms.ContextMenuStrip();
                balloonShow = false;
                forceHideTrayWin = true;
                ni.Icon = Properties.Resources.AppIcon;
                ni.Visible = true;
                ni.DoubleClick += Ni_DoubleClick;
                ni.MouseMove += Ni_MouseMove;
                cm.Items.Add("Open", null, Cm_Open_Click);
                cm.Items.Add("Exit", null, Cm_Exit_Click);
                ni.ContextMenuStrip = cm;

                Task.Run(CheckMousePos);
            }
        }

        private async void CheckMousePos()
        {
            while(trayWin != null)
            {
                if (Forms.Cursor.Position != p)
                {
                    if (trayWin != null && trayWin.Visibility == Visibility.Visible)
                    {
                        await Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                      {

                          trayWin.Visibility = Visibility.Hidden;
                      }));
                    }
                }
                await Task.Delay(500);
            }
        }

        private void Ni_MouseMove(object sender, Forms.MouseEventArgs e)
        {
            if (!forceHideTrayWin)
            {
                p = Forms.Cursor.Position;
                if (trayWin.Visibility == Visibility.Hidden)
                {
                    trayWin.Topmost = true;
                    trayWin.Left = p.X - trayWin.Width;
                    trayWin.Top = p.Y - trayWin.Height;
                    trayWin.Visibility = Visibility.Visible;
                }
            }
            else
                forceHideTrayWin = false;
        }

        private void Cm_Open_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void Cm_Exit_Click(object sender, EventArgs e)
        {
            cm.Dispose();
            ni.MouseMove -= Ni_MouseMove;
            ni.Dispose();
            trayWin.Close();
            aboutWin.Close();
            if (isSingleInstance)
                mutex.Close();
            this.Close();
        }

        private void Ni_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Minimize_Button_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        private void Exit_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!balloonShow)
            {
                ni.ShowBalloonTip(1000, null, "Minimized to system tray", Forms.ToolTipIcon.None);
                balloonShow = true;
            }
            forceHideTrayWin = true;
            aboutWin.Hide();
            this.Hide();
        }

        private void About_Button_Click(object sender, RoutedEventArgs e)
        {
            aboutWin.Show(this);
        }
    }
}
