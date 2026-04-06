using System;

namespace OpenNetMeter.PlatformAbstractions;

public interface IUiDispatcher
{
    bool CheckAccess();
    void Post(Action action);
}
