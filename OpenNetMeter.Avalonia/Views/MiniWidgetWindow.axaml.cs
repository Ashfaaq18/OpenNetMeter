using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using OpenNetMeter.Avalonia.ViewModels;
using OpenNetMeter.Utilities;

namespace OpenNetMeter.Avalonia.Views;

public partial class MiniWidgetWindow : Window
{
    public MiniWidgetWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void WidgetChrome_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (DataContext is MiniWidgetViewModel { IsPinned: true })
            return;

        try
        {
            BeginMoveDrag(e);
        }
        catch (System.Exception ex)
        {
            EventLogger.Error("Failed to drag mini widget window", ex);
        }
    }
}
