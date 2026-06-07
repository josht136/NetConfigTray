using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetConfigTray.Helpers;
using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class NetworkInfoService
{
    private readonly ConnectedDeviceService _connectedDeviceService = new();

    public IReadOnlyList<NetworkInterfaceInfo> GetActiveInterfaces()
    {
        var configurations = QueryIpConfigurations();
        var adapters = QueryAdapters();
        var primaryInterfaceIndex = QueryPrimaryInterfaceIndex();
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

            if (!TryGetIpv4Details(ni, out var ipv4, out var cidr))
            {
                continue;
            }

            TryResolveAdapter(ni, adapters, out var adapter);
            if (!TryGetConfiguration(ni, adapter, ipv4, configurations, out var config))
            {
                continue;
            }

            var name = ResolveFriendlyName(ni, adapter, config.Description);
            var gateway = GetGateway(ni);
            var stats = ni.GetIPv4Statistics();
            var interfaceIndex = ni.GetIPProperties().GetIPv4Properties()?.Index;

            results.Add(new NetworkInterfaceInfo
            {
                Id = ni.Id,
                Name = name,
                IPv4Address = ipv4,
                Cidr = cidr,
                MacAddress = FormatHelper.FormatMacAddress(ni.GetPhysicalAddress()),
                LinkSpeedBps = ni.Speed,
                ConfigurationType = config.DhcpEnabled ? IpConfigurationType.Dhcp : IpConfigurationType.Static,
                Gateway = gateway,
                DnsServers = GetDnsServers(ni),
                InterfaceType = ni.NetworkInterfaceType,
                BytesReceived = stats.BytesReceived,
                BytesSent = stats.BytesSent,
                ConnectedDevice = _connectedDeviceService.GetConnectedDevice(ni, gateway),
                IsPrimary = interfaceIndex is not null && primaryInterfaceIndex == (uint)interfaceIndex.Value
            });
        }

        if (results.Count > 0 && results.All(i => !i.IsPrimary))
        {
            results[0] = results[0] with { IsPrimary = true };
        }

        return results
            .OrderByDescending(i => i.IsPrimary)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public NetworkInterfaceInfo? GetPrimaryInterface()
    {
        return GetActiveInterfaces().FirstOrDefault(i => i.IsPrimary)
            ?? GetActiveInterfaces().FirstOrDefault();
    }

    private static string GetGateway(NetworkInterface networkInterface)
    {
        foreach (var gateway in networkInterface.GetIPProperties().GatewayAddresses)
        {
            if (gateway.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                return gateway.Address.ToString();
            }
        }

        return string.Empty;
    }

    private static string GetDnsServers(NetworkInterface networkInterface)
    {
        var servers = networkInterface.GetIPProperties().DnsAddresses
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.ToString())
            .ToArray();

        return servers.Length == 0 ? "None" : string.Join(", ", servers);
    }

    private static uint? QueryPrimaryInterfaceIndex()
    {
        uint? bestIndex = null;
        uint bestMetric = uint.MaxValue;

        using var searcher = new ManagementObjectSearcher(
            "SELECT InterfaceIndex, Metric1 FROM Win32_IP4RouteTable WHERE Destination='0.0.0.0' AND Mask='0.0.0.0'");

        foreach (var obj in searcher.Get().Cast<ManagementObject>())
        {
            var metric = ReadUInt32(obj["Metric1"]);
            var interfaceIndex = ReadUInt32(obj["InterfaceIndex"]);
            if (metric is null || interfaceIndex is null)
            {
                continue;
            }

            if (metric.Value < bestMetric)
            {
                bestMetric = metric.Value;
                bestIndex = interfaceIndex.Value;
            }
        }

        return bestIndex;
    }

    private static bool TryGetIpv4Details(
        NetworkInterface networkInterface,
        out string ipv4,
        out string cidr)
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

            ipv4 = address.Address.ToString();
            cidr = $"{ipv4}/{address.PrefixLength}";
            return true;
        }

        ipv4 = string.Empty;
        cidr = string.Empty;
        return false;
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
