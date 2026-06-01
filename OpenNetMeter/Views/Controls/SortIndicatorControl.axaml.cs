using Avalonia;
using Avalonia.Controls;

namespace OpenNetMeter.Views.Controls;

public partial class SortIndicatorControl : UserControl
{
    public static readonly StyledProperty<string> ColumnNameProperty =
        AvaloniaProperty.Register<SortIndicatorControl, string>(nameof(ColumnName));

    public static readonly StyledProperty<string?> CurrentSortColumnProperty =
        AvaloniaProperty.Register<SortIndicatorControl, string?>(nameof(CurrentSortColumn));

    public static readonly StyledProperty<bool> IsSortDescendingProperty =
        AvaloniaProperty.Register<SortIndicatorControl, bool>(nameof(IsSortDescending));

    public static readonly DirectProperty<SortIndicatorControl, bool> IsAscVisibleProperty =
        AvaloniaProperty.RegisterDirect<SortIndicatorControl, bool>(nameof(IsAscVisible), o => o.IsAscVisible);

    public static readonly DirectProperty<SortIndicatorControl, bool> IsDescVisibleProperty =
        AvaloniaProperty.RegisterDirect<SortIndicatorControl, bool>(nameof(IsDescVisible), o => o.IsDescVisible);

    public static readonly DirectProperty<SortIndicatorControl, bool> IsUnsortedVisibleProperty =
        AvaloniaProperty.RegisterDirect<SortIndicatorControl, bool>(nameof(IsUnsortedVisible), o => o.IsUnsortedVisible);

    private bool _isAscVisible;
    private bool _isDescVisible;
    private bool _isUnsortedVisible = true;

    public string ColumnName
    {
        get => GetValue(ColumnNameProperty);
        set => SetValue(ColumnNameProperty, value);
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

    public bool IsAscVisible
    {
        get => _isAscVisible;
        private set => SetAndRaise(IsAscVisibleProperty, ref _isAscVisible, value);
    }

    public bool IsDescVisible
    {
        get => _isDescVisible;
        private set => SetAndRaise(IsDescVisibleProperty, ref _isDescVisible, value);
    }

    public bool IsUnsortedVisible
    {
        get => _isUnsortedVisible;
        private set => SetAndRaise(IsUnsortedVisibleProperty, ref _isUnsortedVisible, value);
    }

    public SortIndicatorControl()
    {
        InitializeComponent();
        UpdateVisibility();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColumnNameProperty ||
            change.Property == CurrentSortColumnProperty ||
            change.Property == IsSortDescendingProperty)
        {
            UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        var isSorted = string.Equals(ColumnName, CurrentSortColumn, System.StringComparison.Ordinal);
        IsAscVisible = isSorted && !IsSortDescending;
        IsDescVisible = isSorted && IsSortDescending;
        IsUnsortedVisible = !isSorted;
    }
}
