using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace WhereIsMyData.Views
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        private bool IsMainAppExit;
        public AboutWindow()
        {
            InitializeComponent();
            IsMainAppExit = false;
        }
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        public void SetAppExit()
        {
            IsMainAppExit = true;
        }
        private void Exit_Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            if (IsMainAppExit)
                e.Cancel = false;
            else
            {
                e.Cancel = true;
                Hide();
            } 
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
