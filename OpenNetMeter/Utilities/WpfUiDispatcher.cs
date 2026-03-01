using System;
using System.Windows.Threading;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Utilities
{
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        private readonly Dispatcher dispatcher;

        public WpfUiDispatcher(Dispatcher? dispatcher = null)
        {
            this.dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        public bool CheckAccess() => dispatcher.CheckAccess();

        public void Post(Action action)
        {
            dispatcher.Invoke(action);
        }
    }
}
