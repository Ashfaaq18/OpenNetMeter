﻿using System.Windows;
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
using System.Windows.Interop;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Mutex? mutex;
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

        private ConfirmationDialog? confDialog;
        private AboutWindow? aboutWin;
        private MiniWidgetV? miniWidget;
        private MainWindowVM? mainWin;
        private DispatcherTimer resizeTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };
        private DispatcherTimer relocationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };

        private Forms.NotifyIcon? trayIcon;
        private bool balloonShow;

        public MainWindow()
        {
            if (IsSingleInstance())
            {
                InitializeComponent();

                confDialog = new ConfirmationDialog(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));     
                aboutWin = new AboutWindow(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
                miniWidget = new MiniWidgetV(this);
                mainWin = new MainWindowVM((MiniWidgetVM)miniWidget.DataContext, (ConfirmationDialogVM)confDialog.DataContext);
                DataContext = mainWin;
                this.Closing += MainWindow_Closing;
                //initialize window position and size
                AllWinPosAndSizeInit();

                //initialize system tray
                trayIcon = new Forms.NotifyIcon();
                Forms.ContextMenuStrip cm = new Forms.ContextMenuStrip();
                balloonShow = false;
                trayIcon.Icon = Properties.Resources.AppIcon;
                trayIcon.Visible = true;
                trayIcon.DoubleClick += Ni_DoubleClick;
                trayIcon.MouseClick += Ni_MouseClick;
                cm.Items.Add("Reset all window positions", null, ResetWinPos_Click);
                cm.Items.Add("Show Mini Widget", null, MiniWidget_Show_Click);
                cm.Items.Add(new Forms.ToolStripSeparator());
                cm.Items.Add("Open", null, Cm_Open_Click);
                cm.Items.Add("Exit", null, Cm_Exit_Click);
                trayIcon.ContextMenuStrip = cm;

                Loaded += MainWindow_Loaded;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if(confDialog != null)
                confDialog.Owner = this;
            if(aboutWin != null)
                aboutWin.Owner = this;
        }

        private void ResetWinPos_Click(object? sender, EventArgs e)
        {
            this.Left = SystemParameters.PrimaryScreenWidth/2 - this.Width / 2;
            this.Top = SystemParameters.PrimaryScreenHeight/2 - this.Height / 2;

            SaveWinPos((int)this.Left, (int)this.Top);

            if(miniWidget != null)
            {
                miniWidget.Left = this.Left + this.Width / 2 - miniWidget.Width / 2;
                miniWidget.Top = this.Top + this.Height / 2 - miniWidget.Height / 2;

                miniWidget.SaveWinPos((int)miniWidget.Left, (int)miniWidget.Top);
            }       
        }

        private void MiniWidget_Show_Click(object? sender, EventArgs e)
        {
            WindowInteropHelper miniWidgetHwnd = new WindowInteropHelper(miniWidget);
            if (miniWidgetHwnd.Handle != IntPtr.Zero && miniWidget != null)
            {
                miniWidget.ShowMiniWidget();
            } 
        }

        // this is for when the user clicks the window exit button through the alt+tab program switcher
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            e.Cancel = true;
            this.Visibility = Visibility.Collapsed;
        }
        private void Ni_MouseClick(object? sender, Forms.MouseEventArgs e)
        {
            switch (e.Button)
            {
                case Forms.MouseButtons.Right:
                    if(trayIcon != null)
                    {
                        if (Properties.Settings.Default.DarkMode)
                            trayIcon.ContextMenuStrip.ForeColor = Color.White;
                        else
                            trayIcon.ContextMenuStrip.ForeColor = Color.Black;
                        trayIcon.ContextMenuStrip.Renderer = new CustomSystemTray();
                    }
                    break;
            }
        }

        private void AllWinPosAndSizeInit()
        {
            if (Properties.Settings.Default.LaunchFirstTime)
            {
                Properties.Settings.Default.WinSize = new System.Drawing.Size((int)this.MinWidth, (int)this.MinHeight);
                this.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                Properties.Settings.Default.WinPos = new System.Drawing.Point((int)this.Left, (int)this.Top);

                if(miniWidget != null)
                {
                    miniWidget.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                    Properties.Settings.Default.MiniWidgetPos = new System.Drawing.Point((int)miniWidget.Left, (int)miniWidget.Top);
                }

                Properties.Settings.Default.LaunchFirstTime = false;
                Properties.Settings.Default.Save();
            }

            this.Left = Properties.Settings.Default.WinPos.X;
            this.Top = Properties.Settings.Default.WinPos.Y;
            this.Width = Properties.Settings.Default.WinSize.Width;
            this.Height = Properties.Settings.Default.WinSize.Height;

            if(miniWidget!= null)
            {
                miniWidget.Left = Properties.Settings.Default.MiniWidgetPos.X;
                miniWidget.Top = Properties.Settings.Default.MiniWidgetPos.Y;
            }

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

        private void Cm_Open_Click(object? sender, EventArgs e)
        {
            this.Visibility = Visibility.Visible;
            this.Activate();
        }

        private void Cm_Exit_Click(object? sender, EventArgs e)
        {
            if(confDialog != null)
                confDialog.Close();
            if (miniWidget != null)
                miniWidget.Close();
            if (aboutWin != null)
                aboutWin.Close();
            this.Closing -= MainWindow_Closing;
            this.Close();
            if(trayIcon != null)
                trayIcon.Visible = false;
        }

        private void Ni_DoubleClick(object? sender, EventArgs e)
        {
            this.Visibility = Visibility.Visible;
            this.Activate();
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                double fullScreenWidth = this.RenderSize.Width;
                double fullScreenHeight = this.RenderSize.Height;
                WindowState = WindowState.Normal;
                this.Left = e.GetPosition(this).X - (this.RenderSize.Width / fullScreenWidth) * e.GetPosition(this).X;
                this.Top = e.GetPosition(this).Y - (this.RenderSize.Height / fullScreenHeight) * e.GetPosition(this).Y;
            }

            DragMove();
        }

        private void Minimize_Button_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        
        public void Exit_Button_Click(object? sender, RoutedEventArgs? e)
        {
            if (!balloonShow && trayIcon != null)
            {
                trayIcon.ShowBalloonTip(1000, null, "Minimized to system tray", Forms.ToolTipIcon.None);
                balloonShow = true;
            }
            if(aboutWin != null)
                aboutWin.Visibility = Visibility.Collapsed;
            if(confDialog != null)
                confDialog.Visibility = Visibility.Collapsed;
            this.Visibility = Visibility.Collapsed;
        }



        private void About_Button_Click(object sender, RoutedEventArgs e)
        {
            if(aboutWin != null)
                aboutWin.Visibility = Visibility.Visible;
        }

        //save window size and position at the end of the respective events
        private void ResizeTimer_Tick(object? sender, EventArgs e)
        {
            resizeTimer.IsEnabled = false;

            //Do end of resize processing
            Properties.Settings.Default.WinSize =  new System.Drawing.Size((int)this.Width, (int)this.Height);
            Properties.Settings.Default.Save();

            //pass parent window dimensions to confirmation dialog
            if(confDialog != null)
                confDialog.SetParentWindowRect(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
            if (aboutWin != null)
                aboutWin.SetParentWindowRect(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
        }

        private void MyWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            resizeTimer.IsEnabled = true;
            resizeTimer.Stop();
            resizeTimer.Start();
        }
        
        private void RelocationTimer_Tick(object? sender, EventArgs e)
        {
            relocationTimer.IsEnabled = false;

            //Do end of relocation processing
            SaveWinPos((int)this.Left, (int)this.Top);
        }

        private void SaveWinPos(int x, int y)
        {
            Properties.Settings.Default.WinPos = new System.Drawing.Point(x, y);
            Properties.Settings.Default.Save();

            //pass parent window dimensions to confirmation and about dialog
            if (confDialog != null)
                confDialog.SetParentWindowRect(new System.Windows.Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight));
            if (aboutWin != null)
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
