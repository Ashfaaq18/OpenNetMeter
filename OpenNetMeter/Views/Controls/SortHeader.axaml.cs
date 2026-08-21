using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace OpenNetMeter.Views.Controls;

public partial class SortHeader : UserControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SortHeader, string?>(nameof(Header));

    public static readonly StyledProperty<string?> SortKeyProperty =
        AvaloniaProperty.Register<SortHeader, string?>(nameof(SortKey));

    public static readonly StyledProperty<string?> CurrentSortColumnProperty =
        AvaloniaProperty.Register<SortHeader, string?>(nameof(CurrentSortColumn));

    public static readonly StyledProperty<bool> IsSortDescendingProperty =
        AvaloniaProperty.Register<SortHeader, bool>(nameof(IsSortDescending));

    public static readonly StyledProperty<ICommand?> SortCommandProperty =
        AvaloniaProperty.Register<SortHeader, ICommand?>(nameof(SortCommand));

    public SortHeader()
    {
        InitializeComponent();
    }

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string? SortKey
    {
        get => GetValue(SortKeyProperty);
        set => SetValue(SortKeyProperty, value);
    }

    public string? CurrentSortColumn
    {
        get => GetValue(CurrentSortColumnProperty);
        set => SetValue(CurrentSortColumnProperty, value);
    }

    public bool IsSortDescending
    {
        get => GetValue(IsSortDescendingProperty);
        set => SetValue(IsSortDescendingProperty, value);
    }

    public ICommand? SortCommand
    {
        get => GetValue(SortCommandProperty);
        set => SetValue(SortCommandProperty, value);
    }
}
