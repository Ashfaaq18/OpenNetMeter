using System;
using Avalonia.Controls;

namespace OpenNetMeter.Services;

public interface ITrayNotificationService : IDisposable
{
    void ShowMinimizedToTrayOnce(Window mainWindow);
}
