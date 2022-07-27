using System;
using System.Windows.Input;

namespace OpenNetMeter.Utilities
{
    public class BaseCommand : ICommand
    {
        private Action<object?> action;
        private bool canExecute;
        public BaseCommand(Action<object?> action, bool canExecute)
        {
            this.action = action;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public void Execute(object? parameter)
        {
            action(parameter);
        }
    }
}
