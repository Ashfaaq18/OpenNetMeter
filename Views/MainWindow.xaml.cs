using System.Windows;
using WhereIsMyData.ViewModels;
using System.Windows.Input;

namespace WhereIsMyData.Views
{
    /// <summary>
    /// Interaction logic for HomeUI.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AboutWindow aboutWin;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new NavigationAndTasksVM();
            aboutWin = new AboutWindow();
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
            aboutWin.SetAppExit();
            aboutWin.Close();
            Close();
        }

        private void About_Button_Click(object sender, RoutedEventArgs e)
        {
            aboutWin.Show();
        }
    }
}
