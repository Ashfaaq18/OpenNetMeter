using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            this.Resources.Add("AppVersion", "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            InitializeComponent();
        }
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        public void CloseWin()
        {
            Close();
        }
        private void Exit_Button_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessStartInfo psi = new ProcessStartInfo(e.Uri.AbsoluteUri);
            psi.UseShellExecute = true;
            Process.Start(psi);
            e.Handled = true;
        }

        public void Show(Window owner)
        {
            this.Owner = owner;
            //always centre the about window to the parent window
            this.Left = owner.Left + owner.Width / 2 - this.Width/2;
            this.Top = owner.Top + owner.Height / 2 - this.Height/2;
            this.Show();
        }
    }
}
