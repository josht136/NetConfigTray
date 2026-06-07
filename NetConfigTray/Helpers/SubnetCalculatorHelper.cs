using System.Net;
using System.Net.Sockets;
using NetConfigTray.Models;

namespace NetConfigTray.Helpers;

public static class SubnetCalculatorHelper
{
    public static SubnetInfo? Calculate(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var ipAddress) ||
            !int.TryParse(parts[1], out var prefixLength) ||
            ipAddress.AddressFamily != AddressFamily.InterNetwork ||
            prefixLength is < 0 or > 32)
        {
            return null;
        }

        var ip = IpToUInt32(ipAddress);
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        var network = ip & mask;
        var broadcast = network | ~mask;
        var firstHost = prefixLength >= 31 ? network : network + 1;
        var lastHost = prefixLength >= 31 ? broadcast : broadcast - 1;
        var usableHosts = prefixLength >= 31
            ? (int)(broadcast - network + 1)
            : Math.Max(0, (int)(broadcast - network - 1));

        return new SubnetInfo
        {
            NetworkAddress = UInt32ToIp(network),
            BroadcastAddress = UInt32ToIp(broadcast),
            FirstHost = UInt32ToIp(firstHost),
            LastHost = UInt32ToIp(lastHost),
            UsableHosts = usableHosts
        };
    }

    /// <summary>
    /// Enumerates host IPs from <paramref name="firstHost"/> to <paramref name="lastHost"/>
    /// inclusive, capped at <paramref name="limit"/> addresses.
    /// </summary>
    public static List<string> EnumerateHostRange(string firstHost, string lastHost, int limit)
    {
        var hosts = new List<string>();
        if (!IPAddress.TryParse(firstHost, out var first) ||
            !IPAddress.TryParse(lastHost, out var last) ||
            first.AddressFamily != AddressFamily.InterNetwork ||
            last.AddressFamily != AddressFamily.InterNetwork)
        {
            return hosts;
        }

        var start = IpToUInt32(first);
        var end = IpToUInt32(last);
        if (end < start)
        {
            return hosts;
        }

        var count = Math.Min((long)(end - start) + 1, limit);
        for (long i = 0; i < count; i++)
        {
            hosts.Add(UInt32ToIp(start + (uint)i));
        }

        return hosts;
    }

    private static uint IpToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static string UInt32ToIp(uint value)
    {
        return new IPAddress(new byte[]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        }).ToString();
    }
}
