using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class NetworkSnapshotService
{
    private readonly NetworkInfoService _networkInfo = new();
    private readonly object _lock = new();
    private IReadOnlyList<NetworkInterfaceInfo> _snapshot = Array.Empty<NetworkInterfaceInfo>();
    private NetworkInterfaceInfo? _primary;
    private int _refreshInProgress;

    public event Action? SnapshotUpdated;

    public IReadOnlyList<NetworkInterfaceInfo> GetSnapshot()
    {
        lock (_lock)
        {
            return _snapshot;
        }
    }

    public NetworkInterfaceInfo? GetPrimaryInterface()
    {
        lock (_lock)
        {
            return _primary;
        }
    }

    public void EnsureFresh(TimeSpan maxAge, bool includeConnectedDevices = false)
    {
        lock (_lock)
        {
            if (_snapshot.Count > 0 && DateTime.UtcNow - _lastRefresh <= maxAge)
            {
                return;
            }
        }

        RequestRefresh(includeConnectedDevices);
    }

    public void RequestRefresh(bool includeConnectedDevices = false)
    {
        if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0)
        {
            return;
        }

        Task.Run(() =>
        {
            try
            {
                var interfaces = _networkInfo.GetActiveInterfaces(includeConnectedDevices);
                var primary = interfaces.FirstOrDefault(i => i.IsPrimary)
                    ?? interfaces.FirstOrDefault();

                lock (_lock)
                {
                    _snapshot = interfaces;
                    _primary = primary;
                    _lastRefresh = DateTime.UtcNow;
                }

                SnapshotUpdated?.Invoke();
            }
            finally
            {
                Interlocked.Exchange(ref _refreshInProgress, 0);
            }
        });
    }

    public Dictionary<string, (long BytesReceived, long BytesSent)> GetLiveByteCounts()
    {
        return _networkInfo.GetLiveByteCounts();
    }

    public void RequestConnectedDevice(string interfaceId)
    {
        Task.Run(() =>
        {
            var updated = _networkInfo.RefreshConnectedDevice(interfaceId);
            if (updated is null)
            {
                return;
            }

            lock (_lock)
            {
                _snapshot = _snapshot
                    .Select(info => info.Id == interfaceId ? updated : info)
                    .ToList();
            }

            SnapshotUpdated?.Invoke();
        });
    }

    private DateTime _lastRefresh = DateTime.MinValue;
}
