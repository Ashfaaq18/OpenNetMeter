using System.Windows;
using WhereIsMyData.ViewModels;
using System.Windows.Input;
using Forms = System.Windows.Forms;
using System;

namespace WhereIsMyData.Views
{
    /// <summary>
    /// Interaction logic for HomeUI.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AboutWindow aboutWin;
        private Forms.NotifyIcon ni;
        private Forms.ContextMenuStrip cm;
        private Forms.MenuStrip ms;
        public MainWindow()
        {
            InitializeComponent();
            
            //initialize system tray
            ni = new Forms.NotifyIcon();
            cm = new Forms.ContextMenuStrip();
            ms = new Forms.MenuStrip();

            ni.Icon = new System.Drawing.Icon("Resources/myicon1.ico");
            ni.Visible = true;
            ni.DoubleClick += Ni_DoubleClick;
            cm.Items.Add("Open", null, Cm_Open_Click);
            cm.Items.Add("Exit", null, Cm_Exit_Click);

            ni.ContextMenuStrip = cm;

            DataContext = new NavigationAndTasksVM();
            aboutWin = new AboutWindow();
        }

        private void Cm_Open_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void Cm_Exit_Click(object sender, System.EventArgs e)
        {
            ms.Dispose();
            cm.Dispose();
            ni.Dispose();
            aboutWin.Close();
            this.Close();
        }

        private void Ni_DoubleClick(object sender, System.EventArgs e)
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
            aboutWin.Hide();
            this.Hide();
        }

        private void About_Button_Click(object sender, RoutedEventArgs e)
        {
            aboutWin.Show(this);
        }
    }
}
