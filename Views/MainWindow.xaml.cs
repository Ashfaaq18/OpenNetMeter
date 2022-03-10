using System.Windows;
using OpenNetMeter.ViewModels;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing;
using OpenNetMeter.Models;
using System.Windows.Threading;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Mutex mutex;
        public bool IsSingleInstance()
        {
            bool createdNew;
            mutex = new Mutex(true, "{6C4919CA-062E-47E3-85CC-2393D00CBA4A}", out createdNew);

            if (!createdNew)
            {
                //exit app
                MessageBox.Show("An instance is already running,\nCheck if it's minimized to the system tray", "OpenNetMeter", MessageBoxButton.OK);
                
                Application.Current.Shutdown();
                return false;
            }
            else
                return true;
        }

        private AboutWindow aboutWin;
        private TrayPopupWinV trayWin;
        private NavigationAndTasksVM navWin;
        private DispatcherTimer resizeTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };
        private DispatcherTimer relocationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };

        private Forms.NotifyIcon ni;
        private Forms.ContextMenuStrip cm;
        private bool balloonShow;
        private bool forceHideTrayWin;
        private System.Drawing.Point p;
        public MainWindow()
        {
            if (IsSingleInstance())
            {
                InitializeComponent();

                trayWin = new TrayPopupWinV();
                navWin = new NavigationAndTasksVM((TrayPopupVM)trayWin.DataContext);
                DataContext = navWin;
                aboutWin = new AboutWindow();

                //initialize window position and size
                if(Properties.Settings.Default.LaunchFirstTime)
                {
                    Properties.Settings.Default.WinSize = new System.Drawing.Size((int)this.MinWidth, (int)this.MinHeight);
                    this.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                    Properties.Settings.Default.WinPos = new System.Drawing.Point((int)this.Left, (int)this.Top);
                    Properties.Settings.Default.LaunchFirstTime = false;
                    Properties.Settings.Default.Save();
                }

                this.Left = Properties.Settings.Default.WinPos.X;
                this.Top = Properties.Settings.Default.WinPos.Y;

                this.Width = Properties.Settings.Default.WinSize.Width;
                this.Height = Properties.Settings.Default.WinSize.Height;

                resizeTimer.Tick += ResizeTimer_Tick;
                relocationTimer.Tick += RelocationTimer_Tick;

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
                        await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
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
                    //Shell Tray rectangle
                    IntPtr hWnd = NativeMethods.FindWindowByClassName(IntPtr.Zero, "Shell_TrayWnd");
                    Rectangle shellTrayArea = NativeMethods.GetWindowRectangle(hWnd);

                    //screen rectangle
                    Forms.Screen scrn = Forms.Screen.FromPoint(p);
                    Rectangle workArea = scrn.Bounds;

                    if(shellTrayArea.X == 0 && shellTrayArea.Y == 0) //taskbar pos top or left
                    {
                        if(workArea.Width == shellTrayArea.Width) //top
                        {
                            trayWin.Left = p.X - trayWin.Width;
                            trayWin.Top = p.Y;
                        }
                        else //left
                        {
                            trayWin.Left = p.X;
                            trayWin.Top = p.Y - trayWin.Height;
                        }
                    }
                    else //taskbar pos right or bottom
                    {
                        trayWin.Left = p.X - trayWin.Width;
                        trayWin.Top = p.Y - trayWin.Height;
                    }

                    trayWin.Topmost = true;
                    trayWin.Visibility = Visibility.Visible;
                }
            }
            else
                forceHideTrayWin = false;
        }

        private void Cm_Open_Click(object sender, EventArgs e)
        {
            this.Show();
            this.Activate();
        }

        private void Cm_Exit_Click(object sender, EventArgs e)
        {
            cm.Dispose();
            ni.MouseMove -= Ni_MouseMove;
            ni.Dispose();
            trayWin.Close();
            aboutWin.Close();
            navWin.Dispose();
            mutex.Close();
            this.Close();
        }

        private void Ni_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.Activate();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Minimize_Button_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        public void Exit_Button_Click(object sender, RoutedEventArgs e)
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

        //save window size and position at the end of the respective events
        private void ResizeTimer_Tick(object sender, EventArgs e)
        {
            resizeTimer.IsEnabled = false;

            //Do end of resize processing
            Properties.Settings.Default.WinSize =  new System.Drawing.Size((int)this.Width, (int)this.Height);
            Properties.Settings.Default.Save();
        }

        private void MyWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            resizeTimer.IsEnabled = true;
            resizeTimer.Stop();
            resizeTimer.Start();
        }
        
        private void RelocationTimer_Tick(object sender, EventArgs e)
        {
            relocationTimer.IsEnabled = false;

            //Do end of relocation processing
            Properties.Settings.Default.WinPos = new System.Drawing.Point((int)this.Left, (int)this.Top);
            Properties.Settings.Default.Save();
        }

        private void MyWindow_LocationChanged(object sender, EventArgs e)
        {
            relocationTimer.IsEnabled = true;
            relocationTimer.Stop();
            relocationTimer.Start();
        }
    }
}
