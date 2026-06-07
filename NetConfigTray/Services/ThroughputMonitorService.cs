namespace NetConfigTray.Services;

public sealed class ThroughputMonitorService
{
    private readonly Dictionary<string, Snapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public (long DownloadBps, long UploadBps) GetThroughput(string interfaceId, long bytesReceived, long bytesSent)
    {
        var now = DateTime.UtcNow;

        if (!_snapshots.TryGetValue(interfaceId, out var previous))
        {
            _snapshots[interfaceId] = new Snapshot(bytesReceived, bytesSent, now);
            return (0, 0);
        }

        var elapsedSeconds = (now - previous.Timestamp).TotalSeconds;
        if (elapsedSeconds < 0.1)
        {
            return (0, 0);
        }

        var download = (long)Math.Max(0, (bytesReceived - previous.BytesReceived) / elapsedSeconds);
        var upload = (long)Math.Max(0, (bytesSent - previous.BytesSent) / elapsedSeconds);

        _snapshots[interfaceId] = new Snapshot(bytesReceived, bytesSent, now);
        return (download, upload);
    }

    private readonly record struct Snapshot(long BytesReceived, long BytesSent, DateTime Timestamp);
}
