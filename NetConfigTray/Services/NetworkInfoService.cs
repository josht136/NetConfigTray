using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class NetworkInfoService
{
    public IReadOnlyList<NetworkInterfaceInfo> GetActiveInterfaces()
    {
        var configurations = QueryIpConfigurations();
        var adapters = QueryAdapters();
        var results = new List<NetworkInterfaceInfo>();

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

            var ipv4 = GetPrimaryIPv4(ni);
            if (ipv4 is null)
            {
                continue;
            }

            if (!TryResolveAdapter(ni, adapters, out var adapter))
            {
                continue;
            }

            if (!configurations.TryGetValue(adapter.InterfaceIndex, out var config))
            {
                continue;
            }

            var name = !string.IsNullOrWhiteSpace(adapter.FriendlyName)
                ? adapter.FriendlyName
                : !string.IsNullOrWhiteSpace(config.Description)
                    ? config.Description
                    : ni.Name;

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

    private static bool TryResolveAdapter(
        NetworkInterface networkInterface,
        IReadOnlyList<AdapterInfo> adapters,
        out AdapterInfo adapter)
    {
        var normalizedId = NormalizeGuid(networkInterface.Id);

        foreach (var candidate in adapters)
        {
            if (!string.IsNullOrEmpty(candidate.Guid) &&
                NormalizeGuid(candidate.Guid) == normalizedId)
            {
                adapter = candidate;
                return true;
            }
        }

        foreach (var candidate in adapters)
        {
            if (string.Equals(candidate.Description, networkInterface.Description, StringComparison.OrdinalIgnoreCase))
            {
                adapter = candidate;
                return true;
            }
        }

        adapter = default;
        return false;
    }

    private static string NormalizeGuid(string value)
    {
        return value.Trim('{', '}').ToUpperInvariant();
    }

    private static string? GetPrimaryIPv4(NetworkInterface networkInterface)
    {
        foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
        {
            if (address.Address.AddressFamily != AddressFamily.InterNetwork)
            {
                continue;
            }

            if (IPAddress.IsLoopback(address.Address))
            {
                continue;
            }

            return address.Address.ToString();
        }

        return null;
    }

    private static uint? ReadUInt32(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return Convert.ToUInt32(value);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<uint, (bool DhcpEnabled, string Description)> QueryIpConfigurations()
    {
        var configurations = new Dictionary<uint, (bool DhcpEnabled, string Description)>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Index, Description, DHCPEnabled FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var index = ReadUInt32(obj["Index"]);
            if (index is null)
            {
                continue;
            }

            var dhcpEnabled = obj["DHCPEnabled"] is true;
            var description = obj["Description"] as string ?? string.Empty;

            configurations[index.Value] = (dhcpEnabled, description);
        }

        return configurations;
    }

    private static List<AdapterInfo> QueryAdapters()
    {
        var adapters = new List<AdapterInfo>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT InterfaceIndex, NetConnectionID, GUID, Description FROM Win32_NetworkAdapter");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var interfaceIndex = ReadUInt32(obj["InterfaceIndex"]);
            if (interfaceIndex is null)
            {
                continue;
            }

            adapters.Add(new AdapterInfo(
                interfaceIndex.Value,
                obj["NetConnectionID"] as string ?? string.Empty,
                obj["GUID"] as string ?? string.Empty,
                obj["Description"] as string ?? string.Empty));
        }

        return adapters;
    }

    private readonly record struct AdapterInfo(uint InterfaceIndex, string FriendlyName, string Guid, string Description);
}
