using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class NetworkInfoService
{
    private const int ConnectedStatus = 2;

    public IReadOnlyList<NetworkInterfaceInfo> GetActiveInterfaces()
    {
        var configurations = QueryIpConfigurations();
        var friendlyNames = QueryFriendlyNames();
        var results = new List<NetworkInterfaceInfo>();

        foreach (var (index, config) in configurations)
        {
            if (!IsAdapterConnected(index))
            {
                continue;
            }

            if (!IsInterfaceActive(index))
            {
                continue;
            }

            var ipv4 = GetPrimaryIPv4(config.IpAddresses);
            if (ipv4 is null)
            {
                continue;
            }

            friendlyNames.TryGetValue(index, out var friendlyName);
            var name = !string.IsNullOrWhiteSpace(friendlyName)
                ? friendlyName
                : config.Description;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            results.Add(new NetworkInterfaceInfo
            {
                Name = name,
                IPv4Address = ipv4,
                ConfigurationType = config.DhcpEnabled ? IpConfigurationType.Dhcp : IpConfigurationType.Static
            });
        }

        return results
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsAdapterConnected(uint interfaceIndex)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT NetConnectionStatus FROM Win32_NetworkAdapter WHERE InterfaceIndex = {interfaceIndex}");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            if (obj["NetConnectionStatus"] is ushort status)
            {
                return status == ConnectedStatus;
            }
        }

        return false;
    }

    private static bool IsInterfaceActive(uint interfaceIndex)
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var props = ni.GetIPProperties();
            if (props.GetIPv4Properties()?.Index == (int)interfaceIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetPrimaryIPv4(string[] ipAddresses)
    {
        foreach (var address in ipAddresses)
        {
            if (IPAddress.TryParse(address, out var ip) &&
                ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(ip))
            {
                return address;
            }
        }

        return null;
    }

    private static Dictionary<uint, (bool DhcpEnabled, string[] IpAddresses, string Description)> QueryIpConfigurations()
    {
        var configurations = new Dictionary<uint, (bool DhcpEnabled, string[] IpAddresses, string Description)>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Index, Description, DHCPEnabled, IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            if (obj["Index"] is not uint index)
            {
                continue;
            }

            var dhcpEnabled = obj["DHCPEnabled"] is true;
            var ipAddresses = (obj["IPAddress"] as string[]) ?? Array.Empty<string>();
            var description = obj["Description"] as string ?? string.Empty;

            configurations[index] = (dhcpEnabled, ipAddresses, description);
        }

        return configurations;
    }

    private static Dictionary<uint, string> QueryFriendlyNames()
    {
        var names = new Dictionary<uint, string>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT InterfaceIndex, NetConnectionID FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            if (obj["InterfaceIndex"] is not uint interfaceIndex)
            {
                continue;
            }

            var name = obj["NetConnectionID"] as string;
            if (!string.IsNullOrWhiteSpace(name))
            {
                names[interfaceIndex] = name;
            }
        }

        return names;
    }
}
