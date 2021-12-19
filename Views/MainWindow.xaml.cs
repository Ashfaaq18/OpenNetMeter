using System.Windows;
using WhereIsMyData.ViewModels;

namespace WhereIsMyData.Views
{
    /// <summary>
    /// Interaction logic for HomeUI.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new NavigationAndTasksVM();
        }

        private void Grid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
