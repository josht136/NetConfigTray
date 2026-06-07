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

    public string ConfigurationLabel => ConfigurationType == IpConfigurationType.Dhcp ? "DHCP" : "Static";
}
