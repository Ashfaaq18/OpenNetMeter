using System.Windows;
using OpenNetMeter.ViewModels;

namespace OpenNetMeter.Views
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
