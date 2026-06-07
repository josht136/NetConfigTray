namespace NetConfigTray.Services;

public sealed class AppServices : IDisposable
{
    public NetworkInfoService NetworkInfo { get; } = new();
    public NetworkSnapshotService Snapshot { get; }
    public ThroughputMonitorService Throughput { get; } = new();
    public ThroughputHistoryService ThroughputHistory { get; } = new();
    public GatewayPingService GatewayPing { get; } = new();
    public PublicIpService PublicIp { get; } = new();
    public InterfaceUptimeService Uptime { get; } = new();
    public NetworkChangeNotifierService ChangeNotifier { get; } = new();

    public AppServices()
    {
        Snapshot = new NetworkSnapshotService(NetworkInfo);
    }

    public void Dispose()
    {
        PublicIp.Dispose();
    }
}
