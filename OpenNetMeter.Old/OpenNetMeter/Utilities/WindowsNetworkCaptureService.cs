using System;
using System.ComponentModel;
using OpenNetMeter.Models;
using OpenNetMeter.PlatformAbstractions;

namespace OpenNetMeter.Utilities
{
    public sealed class WindowsNetworkCaptureService : INetworkCaptureService
    {
        private NetworkProcess? networkProcess;
        private readonly object syncLock = new object();
        private bool disposed;

        public event EventHandler<NetworkSnapshotChangedEventArgs>? NetworkChanged;
        public event EventHandler<NetworkTrafficEventArgs>? TrafficObserved;

        public void Start()
        {
            lock (syncLock)
            {
                ThrowIfDisposed();
                if (networkProcess != null)
                    return;

                networkProcess = new NetworkProcess();
                networkProcess.PropertyChanged += NetworkProcess_PropertyChanged;
                networkProcess.Initialize();
            }
        }

        public void Stop()
        {
            lock (syncLock)
            {
                if (networkProcess == null)
                    return;

                networkProcess.PropertyChanged -= NetworkProcess_PropertyChanged;
                networkProcess.Dispose();
                networkProcess = null;
            }
        }

        public void Dispose()
        {
            lock (syncLock)
            {
                if (disposed)
                    return;

                Stop();
                disposed = true;
            }
        }

        private void NetworkProcess_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (networkProcess == null)
                return;

            switch (e.PropertyName)
            {
                case nameof(NetworkProcess.IsNetworkOnline):
                    NetworkChanged?.Invoke(
                        this,
                        new NetworkSnapshotChangedEventArgs(
                            networkProcess.AdapterName,
                            networkProcess.CurrentAdapterId));
                    break;
                case nameof(NetworkProcess.DownloadSpeed):
                    if (networkProcess.DownloadSpeed > 0)
                    {
                        TrafficObserved?.Invoke(
                            this,
                            new NetworkTrafficEventArgs("Aggregate", networkProcess.DownloadSpeed, isReceive: true));
                    }

                    if (networkProcess.UploadSpeed > 0)
                    {
                        TrafficObserved?.Invoke(
                            this,
                            new NetworkTrafficEventArgs("Aggregate", networkProcess.UploadSpeed, isReceive: false));
                    }
                    break;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(WindowsNetworkCaptureService));
        }
    }
}
