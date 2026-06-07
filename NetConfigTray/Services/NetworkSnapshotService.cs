using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class NetworkSnapshotService
{
    private readonly NetworkInfoService _networkInfo;
    private readonly object _lock = new();
    private IReadOnlyList<NetworkInterfaceInfo> _snapshot = Array.Empty<NetworkInterfaceInfo>();
    private NetworkInterfaceInfo? _primary;
    private int _refreshInProgress;
    private bool _queuedRefresh;
    private bool _queuedIncludeSlowDetails;

    public NetworkSnapshotService(NetworkInfoService networkInfo)
    {
        _networkInfo = networkInfo;
    }

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

    public void EnsureFresh(TimeSpan maxAge, bool includeSlowDetails = false)
    {
        lock (_lock)
        {
            if (_snapshot.Count > 0 && DateTime.UtcNow - _lastRefresh <= maxAge)
            {
                return;
            }
        }

        RequestRefresh(includeSlowDetails);
    }

    public void RequestRefresh(bool includeSlowDetails = false)
    {
        if (includeSlowDetails)
        {
            _queuedIncludeSlowDetails = true;
        }

        if (Interlocked.CompareExchange(ref _refreshInProgress, 1, 0) != 0)
        {
            _queuedRefresh = true;
            return;
        }

        Task.Run(() =>
        {
            var includeSlow = includeSlowDetails || _queuedIncludeSlowDetails;
            _queuedIncludeSlowDetails = false;

            try
            {
                var interfaces = _networkInfo.GetActiveInterfaces(
                    includeConnectedDevices: includeSlow,
                    includeWifiDetails: includeSlow);

                var primary = interfaces.FirstOrDefault(i => i.IsPrimary)
                    ?? interfaces.FirstOrDefault();

                lock (_lock)
                {
                    _snapshot = interfaces;
                    _primary = primary;
                    _lastRefresh = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network refresh failed: {ex}");
            }
            finally
            {
                Interlocked.Exchange(ref _refreshInProgress, 0);
                SnapshotUpdated?.Invoke();

                if (_queuedRefresh || _queuedIncludeSlowDetails)
                {
                    _queuedRefresh = false;
                    var slow = _queuedIncludeSlowDetails;
                    _queuedIncludeSlowDetails = false;
                    RequestRefresh(slow);
                }
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
            try
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connected device refresh failed: {ex}");
            }
        });
    }

    private DateTime _lastRefresh = DateTime.MinValue;
}
