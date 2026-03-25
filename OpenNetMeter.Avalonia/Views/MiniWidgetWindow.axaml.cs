using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
}
