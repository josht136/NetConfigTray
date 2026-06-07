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

            TryResolveAdapter(ni, adapters, out var adapter);
            if (!TryGetConfiguration(ni, adapter, ipv4, configurations, out var config))
            {
                continue;
            }

            var name = ResolveFriendlyName(ni, adapter, config.Description);

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

    private static string ResolveFriendlyName(
        NetworkInterface networkInterface,
        AdapterInfo? adapter,
        string configDescription)
    {
        if (adapter is { FriendlyName: { Length: > 0 } friendlyName })
        {
            return friendlyName;
        }

        if (!string.IsNullOrWhiteSpace(configDescription))
        {
            return configDescription;
        }

        if (!string.IsNullOrWhiteSpace(networkInterface.Name))
        {
            return networkInterface.Name;
        }

        return networkInterface.Description;
    }

    private static bool TryGetConfiguration(
        NetworkInterface networkInterface,
        AdapterInfo? adapter,
        string ipv4,
        IReadOnlyDictionary<uint, ConfigurationInfo> configurations,
        out ConfigurationInfo config)
    {
        if (adapter is not null && configurations.TryGetValue(adapter.Value.AdapterIndex, out config))
        {
            return true;
        }

        foreach (var candidate in configurations.Values)
        {
            if (candidate.IpAddresses.Contains(ipv4, StringComparer.OrdinalIgnoreCase))
            {
                config = candidate;
                return true;
            }
        }

        foreach (var candidate in configurations.Values)
        {
            if (DescriptionsMatch(candidate.Description, networkInterface.Description))
            {
                config = candidate;
                return true;
            }
        }

        if (adapter is not null)
        {
            foreach (var candidate in configurations.Values)
            {
                if (DescriptionsMatch(candidate.Description, adapter.Value.Description))
                {
                    config = candidate;
                    return true;
                }
            }
        }

        config = default;
        return false;
    }

    private static bool TryResolveAdapter(
        NetworkInterface networkInterface,
        IReadOnlyList<AdapterInfo> adapters,
        out AdapterInfo? adapter)
    {
        var ipv4Index = networkInterface.GetIPProperties().GetIPv4Properties()?.Index;

        if (ipv4Index is not null)
        {
            foreach (var candidate in adapters)
            {
                if (candidate.InterfaceIndex == (uint)ipv4Index.Value)
                {
                    adapter = candidate;
                    return true;
                }
            }
        }

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
            if (DescriptionsMatch(candidate.Description, networkInterface.Description))
            {
                adapter = candidate;
                return true;
            }
        }

        adapter = null;
        return false;
    }

    private static bool DescriptionsMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            || left.Contains(right, StringComparison.OrdinalIgnoreCase)
            || right.Contains(left, StringComparison.OrdinalIgnoreCase);
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

            if (address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
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

    private static Dictionary<uint, ConfigurationInfo> QueryIpConfigurations()
    {
        var configurations = new Dictionary<uint, ConfigurationInfo>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Index, Description, DHCPEnabled, IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var index = ReadUInt32(obj["Index"]);
            if (index is null)
            {
                continue;
            }

            var dhcpEnabled = obj["DHCPEnabled"] is true;
            var description = obj["Description"] as string ?? string.Empty;
            var ipAddresses = (obj["IPAddress"] as string[]) ?? Array.Empty<string>();

            configurations[index.Value] = new ConfigurationInfo(dhcpEnabled, description, ipAddresses);
        }

        return configurations;
    }

    private static List<AdapterInfo> QueryAdapters()
    {
        var adapters = new List<AdapterInfo>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT Index, InterfaceIndex, NetConnectionID, GUID, Description FROM Win32_NetworkAdapter");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var adapterIndex = ReadUInt32(obj["Index"]);
            var interfaceIndex = ReadUInt32(obj["InterfaceIndex"]);
            if (adapterIndex is null || interfaceIndex is null)
            {
                continue;
            }

            adapters.Add(new AdapterInfo(
                adapterIndex.Value,
                interfaceIndex.Value,
                obj["NetConnectionID"] as string ?? string.Empty,
                obj["GUID"] as string ?? string.Empty,
                obj["Description"] as string ?? string.Empty));
        }

        return adapters;
    }

    private readonly record struct AdapterInfo(
        uint AdapterIndex,
        uint InterfaceIndex,
        string FriendlyName,
        string Guid,
        string Description);

    private readonly record struct ConfigurationInfo(bool DhcpEnabled, string Description, string[] IpAddresses);
}
