using System.Net.NetworkInformation;

namespace NetConfigTray.Models;

public enum IpConfigurationType
{
    Dhcp,
    Static
}

public sealed record NetworkInterfaceInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string IPv4Address { get; init; }
    public required string Cidr { get; init; }
    public required string MacAddress { get; init; }
    public required long LinkSpeedBps { get; init; }
    public required IpConfigurationType ConfigurationType { get; init; }
    public required string Gateway { get; init; }
    public required string DnsServers { get; init; }
    public required NetworkInterfaceType InterfaceType { get; init; }
    public required long BytesReceived { get; init; }
    public required long BytesSent { get; init; }
    public ConnectedDeviceInfo? ConnectedDevice { get; init; }
    public bool IsPrimary { get; init; }
    public SubnetInfo? Subnet { get; init; }
    public string? DhcpServer { get; init; }
    public string? DhcpLeaseObtained { get; init; }
    public string? DhcpLeaseExpires { get; init; }
    public uint? RouteMetric { get; init; }
    public string? ConnectionUptime { get; init; }
    public string? GatewayPing { get; init; }
    public string? WifiChannel { get; init; }
    public string? WifiBand { get; init; }
    public string? WifiRadioType { get; init; }

    public string ConfigurationLabel => ConfigurationType == IpConfigurationType.Dhcp ? "DHCP" : "Static";

    public string ChangeSignature =>
        $"{Id}|{IPv4Address}|{ConfigurationType}|{Gateway}|{DhcpLeaseExpires}|{WifiChannel}";
}
