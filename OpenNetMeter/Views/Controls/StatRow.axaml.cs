using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OpenNetMeter.Views.Controls;

public partial class StatRow : UserControl
{
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<StatRow, IBrush?>(nameof(BackgroundBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<StatRow, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<StatRow, IBrush?>(nameof(IconBrush));

    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<StatRow, string?>(nameof(Header));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatRow, string?>(nameof(Value));

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<StatRow, Geometry?>(nameof(IconData));

    public StatRow()
    {
        InitializeComponent();
    }

    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }
}
