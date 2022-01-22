using System.Windows;
using WhereIsMyData.ViewModels;

namespace WhereIsMyData.Views
{
    /// <summary>
    /// Interaction logic for trayPopupWinV.xaml
    /// </summary>
    public partial class TrayPopupWinV : Window
    {
        public TrayPopupWinV()
        {
            InitializeComponent();
            DataContext = new TrayPopupVM();
        }
    }
}
