namespace NetConfigTray.Models;

public enum IpConfigurationType
{
    Dhcp,
    Static
}

public sealed class NetworkInterfaceInfo
{
    public required string Name { get; init; }
    public required string IPv4Address { get; init; }
    public required IpConfigurationType ConfigurationType { get; init; }

    public string ConfigurationLabel => ConfigurationType == IpConfigurationType.Dhcp ? "DHCP" : "Static";
}
