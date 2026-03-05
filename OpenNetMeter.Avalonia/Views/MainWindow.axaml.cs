using Avalonia.Controls;
using Avalonia.Input;
using OpenNetMeter.Avalonia.ViewModels;

namespace OpenNetMeter.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.Dispose();

        base.OnClosed(e);
    }
}
