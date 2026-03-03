using Avalonia.Controls;
using Avalonia.Input;
using OpenNetMeter.Avalonia.ViewModels;

namespace OpenNetMeter.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (sender is not MainWindow window)
            return;

        if (window.DataContext is not MainWindowViewModel vm)
            return;

        vm.RequestMinimizeWindow -= OnRequestMinimizeWindow;
        vm.RequestCloseWindow -= OnRequestCloseWindow;
        vm.RequestAbout -= OnRequestAbout;

        vm.RequestMinimizeWindow += OnRequestMinimizeWindow;
        vm.RequestCloseWindow += OnRequestCloseWindow;
        vm.RequestAbout += OnRequestAbout;
    }

    private void OnRequestMinimizeWindow(object? sender, System.EventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnRequestCloseWindow(object? sender, System.EventArgs e)
    {
        Close();
    }

    private void OnRequestAbout(object? sender, System.EventArgs e)
    {
        // Placeholder until About dialog content is migrated to Avalonia.
    }
}
