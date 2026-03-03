using System.ComponentModel;

namespace OpenNetMeter.Core.ViewModels;

public class MainShellTabsViewModel : INotifyPropertyChanged
{
    private int selectedTabIndex;

    public virtual int SelectedTabIndex
    {
        get => selectedTabIndex;
        set
        {
            if (selectedTabIndex == value)
                return;

            selectedTabIndex = value;
            OnPropertyChanged(nameof(SelectedTabIndex));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
