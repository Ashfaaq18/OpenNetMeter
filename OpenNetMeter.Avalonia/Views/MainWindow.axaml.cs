using Avalonia.Controls;
using OpenNetMeter.Avalonia.ViewModels;

namespace OpenNetMeter.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
