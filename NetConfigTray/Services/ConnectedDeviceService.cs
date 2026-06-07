using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using NetConfigTray.Helpers;
using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class ConnectedDeviceService
{
    private static readonly TimeSpan NetshCacheLifetime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GatewayCacheLifetime = TimeSpan.FromSeconds(60);

    private static string? _cachedNetshOutput;
    private static DateTime _cachedNetshAt = DateTime.MinValue;
    private static readonly object NetshLock = new();

    private readonly Dictionary<string, (ConnectedDeviceInfo Info, DateTime CachedAt)> _gatewayCache = new(StringComparer.OrdinalIgnoreCase);

    public ConnectedDeviceInfo? GetConnectedDevice(NetworkInterface networkInterface, string gateway, bool resolveHostname = false)
    {
        if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
        {
            return GetWifiAccessPoint(networkInterface.Name, gateway);
        }

        if (IsWiredInterface(networkInterface.NetworkInterfaceType))
        {
            return GetWiredUpstreamDevice(gateway, resolveHostname);
        }

        return null;
    }

    private static bool IsWiredInterface(NetworkInterfaceType type)
    {
        return type is NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.FastEthernetT;
    }

    private ConnectedDeviceInfo? GetWiredUpstreamDevice(string gateway, bool resolveHostname)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return null;
        }

        if (_gatewayCache.TryGetValue(gateway, out var cached) &&
            DateTime.UtcNow - cached.CachedAt < GatewayCacheLifetime &&
            (!resolveHostname || cached.Info.Hostname is not null) &&
            (cached.Info.MacAddress is null || cached.Info.Vendor is not null))
        {
            return cached.Info;
        }

        if (!IPAddress.TryParse(gateway, out var gatewayIp))
        {
            return null;
        }

        var mac = ArpHelper.ResolveMacAddress(gatewayIp);
        string? hostname = null;
        string? vendor = null;
        if (resolveHostname)
        {
            hostname = ArpHelper.ResolveHostname(gatewayIp, TimeSpan.FromMilliseconds(750));
            vendor = MacVendorLookup.Resolve(mac);
        }

        var info = new ConnectedDeviceInfo
        {
            // This is the next-hop device on the wire. Any unmanaged switch between the
            // PC and this device is Layer-2 transparent and not separately discoverable.
            Role = "Next-hop device (on the wire)",
            IpAddress = gateway,
            Hostname = hostname,
            MacAddress = mac,
            Vendor = vendor,
            ExtraInfo = mac is null ? "ARP lookup pending — expand or refresh" : null
        };

        _gatewayCache[gateway] = (info, DateTime.UtcNow);
        return info;
    }

    private static ConnectedDeviceInfo? GetWifiAccessPoint(string interfaceName, string gateway)
    {
        var wifiInfo = ParseWifiInterfaceInfo(interfaceName);
        if (wifiInfo is not { } wifi)
        {
            return null;
        }

        return new ConnectedDeviceInfo
        {
            Role = "Access point",
            IpAddress = gateway is { Length: > 0 } ? gateway : null,
            Hostname = wifi.Ssid,
            MacAddress = wifi.Bssid,
            Vendor = MacVendorLookup.Resolve(wifi.Bssid),
            ExtraInfo = wifi.Signal is not null ? $"SSID: {wifi.Ssid} · Signal: {wifi.Signal}" : $"SSID: {wifi.Ssid}"
        };
    }

    private static WifiInterfaceInfo? ParseWifiInterfaceInfo(string interfaceName)
    {
        var output = GetNetshOutput();
        return output is null ? null : ParseWifiOutput(output, interfaceName);
    }

    private static string? GetNetshOutput()
    {
        lock (NetshLock)
        {
            if (_cachedNetshOutput is not null &&
                DateTime.UtcNow - _cachedNetshAt < NetshCacheLifetime)
            {
                return _cachedNetshOutput;
            }
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            if (!process.WaitForExit(1500))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort timeout handling.
                }

                return GetCachedNetshOutput();
            }

            var output = process.StandardOutput.ReadToEnd();
            CacheNetshOutput(output);
            return output;
        }
        catch
        {
            return GetCachedNetshOutput();
        }
    }

    private static string? GetCachedNetshOutput()
    {
        lock (NetshLock)
        {
            return _cachedNetshOutput;
        }
    }

    private static void CacheNetshOutput(string output)
    {
        lock (NetshLock)
        {
            _cachedNetshOutput = output;
            _cachedNetshAt = DateTime.UtcNow;
        }
    }

    private static WifiInterfaceInfo? ParseWifiOutput(string output, string interfaceName)
    {
        var blocks = Regex.Split(output, @"\r?\n\r?\n");
        foreach (var block in blocks)
        {
            var name = ReadValue(block, "Name");
            if (!string.Equals(name, interfaceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ssid = ReadValue(block, "SSID");
            var bssid = ReadValue(block, "BSSID");
            var signal = ReadValue(block, "Signal");

            if (string.IsNullOrWhiteSpace(ssid) && string.IsNullOrWhiteSpace(bssid))
            {
                return null;
            }

            return new WifiInterfaceInfo(ssid ?? "Unknown", bssid, signal);
        }

        return null;
    }

    private static string? ReadValue(string block, string key)
    {
        var match = Regex.Match(block, $@"^\s*{Regex.Escape(key)}\s*:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private readonly record struct WifiInterfaceInfo(string Ssid, string? Bssid, string? Signal);
}
