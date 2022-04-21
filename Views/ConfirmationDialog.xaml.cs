using OpenNetMeter.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for ConfirmationDialog.xaml
    /// </summary>
    public partial class ConfirmationDialog : Window
    {
        private Rect parentWindowRect;
        public ConfirmationDialog(Rect parentWindowRect_param)
        {
            InitializeComponent();
            DataContext = new ConfirmationDialogVM();
            parentWindowRect = parentWindowRect_param;
        }

        public void SetParentWindowRect(Rect parentWindowRect_param)
        {
            parentWindowRect = parentWindowRect_param;
        }

        private void Exit_Button_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Hidden;
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(this.Visibility == Visibility.Visible)
            {
                this.Left = parentWindowRect.Left + (parentWindowRect.Width / 2) - this.ActualWidth / 2;
                this.Top = parentWindowRect.Top + (parentWindowRect.Height / 2) - this.ActualHeight / 2;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = parentWindowRect.Left + (parentWindowRect.Width / 2) - this.ActualWidth / 2;
            this.Top = parentWindowRect.Top + (parentWindowRect.Height / 2) - this.ActualHeight / 2;
        }
    }
}
