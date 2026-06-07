namespace NetConfigTray.Models;

public sealed record SubnetInfo
{
    public required string NetworkAddress { get; init; }
    public required string BroadcastAddress { get; init; }
    public required string FirstHost { get; init; }
    public required string LastHost { get; init; }
    public required int UsableHosts { get; init; }

    public string Summary =>
        $"{NetworkAddress} – {BroadcastAddress} ({UsableHosts} usable hosts)";
}
