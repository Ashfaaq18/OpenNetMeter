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
            DataContext = new NavigationVM();
        }
    }
}
