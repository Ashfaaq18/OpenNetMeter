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
using System.Diagnostics;
using System.Windows.Controls;
using System.ComponentModel;

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

        private ConfirmationDialog confDialog;
        private AboutWindow aboutWin;
        private TrayPopupWinV trayWin;
        private MainWindowVM mainWin;
        private DispatcherTimer resizeTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };
        private DispatcherTimer relocationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };

        private Forms.NotifyIcon ni;
        private Forms.ContextMenuStrip cm;
        private bool balloonShow;
        private bool forceHideTrayWin;
        private System.Drawing.Point p;
        private CancellationTokenSource cts;
        private CancellationToken token;

        public MainWindow()
        {
            if (IsSingleInstance())
            {
                InitializeComponent();

                confDialog = new ConfirmationDialog(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));     
                aboutWin = new AboutWindow(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));        
                trayWin = new TrayPopupWinV();
                mainWin = new MainWindowVM((TrayPopupVM)trayWin.DataContext, (ConfirmationDialogVM)confDialog.DataContext);
                DataContext = mainWin;
                this.Closing += MainWindow_Closing;
                //initialize window position and size
                MainWinPosAndSizeInit();

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
                ni.MouseClick += Ni_MouseClick;
                cm.Items.Add("Open", null, Cm_Open_Click);
                cm.Items.Add("Exit", null, Cm_Exit_Click);
                ni.ContextMenuStrip = cm;
                CheckMousePos();

                SourceInitialized += MainWindow_SourceInitialized;
            }
        }

        // this is for when the user clicks the window exit button through the alt+tab program switcher
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            confDialog.Owner = this;
            aboutWin.Owner = this;
        }

        private void Ni_MouseClick(object sender, Forms.MouseEventArgs e)
        {
            switch (e.Button)
            {
                case Forms.MouseButtons.Right:
                    if (Properties.Settings.Default.DarkMode)
                        ni.ContextMenuStrip.ForeColor = Color.White;
                    else
                        ni.ContextMenuStrip.ForeColor = Color.Black;
                    ni.ContextMenuStrip.Renderer = new CustomSystemTray();
                    break;
            }
        }

        private void MainWinPosAndSizeInit()
        {
            if (Properties.Settings.Default.LaunchFirstTime)
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

            //check if window is out of bounds. This is for, when the user last opened the app in the 2nd monitor and then reopens it with a 1 monitor setup.
            bool isInScreen = false;
            for (int i = 0; i < System.Windows.Forms.Screen.AllScreens.Length; i++)
            {
                //extra margin to repoisition the app when its outside the screen and only its borders are intersecting the edge.
                int margin = 32;
                Rectangle rectA = System.Windows.Forms.Screen.AllScreens[i].WorkingArea;
                if (rectA.Left < (this.Left + this.Width - margin) && (rectA.Left + rectA.Width) > this.Left + margin &&
                    rectA.Top < (this.Top + this.Height - margin) && (rectA.Top + rectA.Height) > this.Top + margin)
                {
                    isInScreen = true;
                }
            }

            //if main window is out of bounds, center it
            if (!isInScreen)
            {
                this.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                Properties.Settings.Default.WinPos = new System.Drawing.Point((int)this.Left, (int)this.Top);
                Properties.Settings.Default.Save();
            }

            resizeTimer.Tick += ResizeTimer_Tick;
            relocationTimer.Tick += RelocationTimer_Tick;
        }
        private void CheckMousePos()
        {
            //init tokens
            cts = new CancellationTokenSource();
            token = cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    Debug.WriteLine("Operation Started : check mouse pos");
                    while (!token.IsCancellationRequested)
                    {
                        //check mouse pos and hide the visible tray win
                        if (Forms.Cursor.Position != p)
                        {
                            if (trayWin.Visibility == Visibility.Visible)
                            {
                                await Application.Current?.Dispatcher?.BeginInvoke((Action)(() =>
                                {
                                    trayWin.Visibility = Visibility.Hidden;
                                }));
                            }
                        }

                        await Task.Delay(500, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation Cancelled : check mouse pos");
                    cts.Dispose();
                    cts = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Critical error: " + ex.Message);
                }
            });
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
            this.Visibility = Visibility.Visible;
            this.Activate();
        }

        private void Cm_Exit_Click(object sender, EventArgs e)
        {
            //stop MainWindowTasks
            if (cts != null)
                cts.Cancel();

            cm.Dispose();
            ni.DoubleClick -= Ni_DoubleClick;
            ni.MouseMove -= Ni_MouseMove;
            ni.MouseClick -= Ni_MouseClick;
            ni.Dispose();
            confDialog.Close();
            trayWin.Close();
            aboutWin.Close();
            mainWin.Dispose();
            mutex.Close();
            this.Closing -= MainWindow_Closing;
            this.Close();
        }

        private void Ni_DoubleClick(object sender, EventArgs e)
        {
            this.Visibility = Visibility.Visible;
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
            aboutWin.Visibility = Visibility.Collapsed;
            confDialog.Visibility = Visibility.Collapsed;
            this.Visibility = Visibility.Collapsed;
        }



        private void About_Button_Click(object sender, RoutedEventArgs e)
        {
            aboutWin.Visibility = Visibility.Visible;
        }

        //save window size and position at the end of the respective events
        private void ResizeTimer_Tick(object sender, EventArgs e)
        {
            resizeTimer.IsEnabled = false;

            //Do end of resize processing
            Properties.Settings.Default.WinSize =  new System.Drawing.Size((int)this.Width, (int)this.Height);
            Properties.Settings.Default.Save();

            //pass parent window dimensions to confirmation dialog
            confDialog.SetParentWindowRect(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
            aboutWin.SetParentWindowRect(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
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

            //pass parent window dimensions to confirmation dialog
            confDialog.SetParentWindowRect(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
            aboutWin.SetParentWindowRect(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
        }

        private void MyWindow_LocationChanged(object sender, EventArgs e)
        {
            relocationTimer.IsEnabled = true;
            relocationTimer.Stop();
            relocationTimer.Start();
        }
    }
}
