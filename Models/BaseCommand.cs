using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WhereIsMyData.Models
{
    public class BaseCommand : ICommand
    {
        private readonly Predicate<object> canExecute;
        private readonly Action<object> action;

        public BaseCommand(Action<object> action)
            : this(action, null)
        {
        }

        public BaseCommand(Action<object> action, Predicate<object> canExecute)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            this.action = action;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return canExecute == null ? true : canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            action(parameter);
        }

        public void DoCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                CanExecuteChanged(this, null);
            }
        }
    }
}
