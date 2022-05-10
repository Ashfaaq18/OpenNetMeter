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
        private DispatcherTimer resizeTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };
        private DispatcherTimer relocationTimer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 200), IsEnabled = false };

        private Window mainWindow;
        private ContextMenuStrip menuStrip;
        public MiniWidgetV(Window mainWindow_ref)
        {
            InitializeComponent();
            DataContext = new MiniWidgetVM();
            menuStrip = new ContextMenuStrip();
            menuStrip.Renderer = new CustomSystemTray();
            menuStrip.Items.Add("Open");
            menuStrip.Items.Add("Hide");
            //menuStrip.
            menuStrip.Visible = true;

            this.Left = Properties.Settings.Default.MiniWidgetPos.X;
            this.Top = Properties.Settings.Default.MiniWidgetPos.Y;

            this.Visibility = Properties.Settings.Default.MiniWidgetVisibility ? Visibility.Visible : Visibility.Collapsed;

            relocationTimer.Tick += RelocationTimer_Tick;

            mainWindow = mainWindow_ref;
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
