using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class NetworkSnapshotService
{
    private readonly NetworkInfoService _networkInfo;
    private readonly object _lock = new();
    private readonly HashSet<string> _connectedDeviceInProgress = new(StringComparer.OrdinalIgnoreCase);
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
            var shouldNotify = false;

            try
            {
                var interfaces = _networkInfo.GetActiveInterfaces(
                    includeConnectedDevices: includeSlow,
                    includeWifiDetails: includeSlow);

                var primary = interfaces.FirstOrDefault(i => i.IsPrimary)
                    ?? interfaces.FirstOrDefault();

                lock (_lock)
                {
                    shouldNotify = !SnapshotsEquivalent(_snapshot, interfaces);
                    _snapshot = interfaces;
                    _primary = primary;
                    _lastRefresh = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Network refresh failed: {ex}");
                shouldNotify = true;
            }
            finally
            {
                Interlocked.Exchange(ref _refreshInProgress, 0);

                if (shouldNotify)
                {
                    SnapshotUpdated?.Invoke();
                }

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
        if (string.IsNullOrWhiteSpace(interfaceId))
        {
            return;
        }

        lock (_lock)
        {
            var existing = _snapshot.FirstOrDefault(info =>
                string.Equals(info.Id, interfaceId, StringComparison.OrdinalIgnoreCase));
            if (existing?.ConnectedDevice is not null)
            {
                return;
            }

            if (!_connectedDeviceInProgress.Add(interfaceId))
            {
                return;
            }
        }

        Task.Run(() =>
        {
            try
            {
                var updated = _networkInfo.RefreshConnectedDevice(interfaceId);
                if (updated is null)
                {
                    return;
                }

                var shouldNotify = false;
                lock (_lock)
                {
                    var current = _snapshot.FirstOrDefault(info =>
                        string.Equals(info.Id, interfaceId, StringComparison.OrdinalIgnoreCase));
                    if (current?.ConnectedDevice is not null &&
                        ConnectedDevicesEquivalent(current.ConnectedDevice, updated.ConnectedDevice))
                    {
                        return;
                    }

                    _snapshot = _snapshot
                        .Select(info => info.Id == interfaceId ? updated : info)
                        .ToList();
                    shouldNotify = true;
                }

                if (shouldNotify)
                {
                    SnapshotUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connected device refresh failed: {ex}");
            }
            finally
            {
                lock (_lock)
                {
                    _connectedDeviceInProgress.Remove(interfaceId);
                }
            }
        });
    }

    private static bool SnapshotsEquivalent(
        IReadOnlyList<NetworkInterfaceInfo> left,
        IReadOnlyList<NetworkInterfaceInfo> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].ChangeSignature, right[i].ChangeSignature, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ConnectedDevicesEquivalent(ConnectedDeviceInfo? left, ConnectedDeviceInfo? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Role == right.Role
            && left.IpAddress == right.IpAddress
            && left.Hostname == right.Hostname
            && left.MacAddress == right.MacAddress
            && left.ExtraInfo == right.ExtraInfo;
    }

    private DateTime _lastRefresh = DateTime.MinValue;
}
