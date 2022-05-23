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
        private Rect parentWindowRect;
        public AboutWindow(Rect parentWindowRect_param)
        {
            this.Resources.Add("AppVersion", "Version: " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            InitializeComponent();
            parentWindowRect = parentWindowRect_param;
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
            this.Visibility = Visibility.Collapsed;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            ProcessStartInfo psi = new ProcessStartInfo(e.Uri.AbsoluteUri);
            psi.UseShellExecute = true;
            Process.Start(psi);
            e.Handled = true;
        }

        public void SetParentWindowRect(Rect parentWindowRect_param)
        {
            parentWindowRect = parentWindowRect_param;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = parentWindowRect.Left + (parentWindowRect.Width / 2) - this.ActualWidth / 2;
            this.Top = parentWindowRect.Top + (parentWindowRect.Height / 2) - this.ActualHeight / 2;
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Left = parentWindowRect.Left + (parentWindowRect.Width / 2) - this.ActualWidth / 2;
                this.Top = parentWindowRect.Top + (parentWindowRect.Height / 2) - this.ActualHeight / 2;
            }
        }
    }
}
