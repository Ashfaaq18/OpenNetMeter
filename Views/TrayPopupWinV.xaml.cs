using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OpenNetMeter.Models;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for trayPopupWinV.xaml
    /// </summary>
    public partial class TrayPopupWinV : Window
    {
        private ContextMenuStrip menuStrip;
        public TrayPopupWinV()
        {
            InitializeComponent();
            DataContext = new TrayPopupVM();
            menuStrip = new ContextMenuStrip();
            menuStrip.Renderer = new CustomSystemTray();
            menuStrip.Items.Add("Open");
            menuStrip.Items.Add("Hide");
            //menuStrip.
            menuStrip.Visible = true;
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MenuItem_Hide_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }
    }
}
