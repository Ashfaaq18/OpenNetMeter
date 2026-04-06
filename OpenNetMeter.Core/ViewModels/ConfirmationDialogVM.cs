using System.ComponentModel;
using System.Windows.Input;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.ViewModels
{
    public class ConfirmationDialogVM : INotifyPropertyChanged
    {
        private UiVisibility isVisible;

        public UiVisibility IsVisible
        {
            get => isVisible;
            set
            {
                isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
            }
        }

        public string? DialogMessage { get; set; }

        public ICommand? BtnCommand { get; set; }

        public ConfirmationDialogVM()
        {
            IsVisible = UiVisibility.Hidden;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
