using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OpenNetMeter.Avalonia.ViewModels;

namespace OpenNetMeter.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void About_Click(object? sender, RoutedEventArgs e)
    {
        // Placeholder until About dialog content is migrated to Avalonia.
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
