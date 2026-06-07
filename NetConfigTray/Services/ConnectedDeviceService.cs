using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using NetConfigTray.Helpers;
using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class ConnectedDeviceService
{
    public ConnectedDeviceInfo? GetConnectedDevice(NetworkInterface networkInterface, string gateway)
    {
        if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
        {
            return GetWifiAccessPoint(networkInterface.Name, gateway);
        }

        if (IsWiredInterface(networkInterface.NetworkInterfaceType))
        {
            return GetWiredUpstreamDevice(gateway);
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

    private static ConnectedDeviceInfo? GetWiredUpstreamDevice(string gateway)
    {
        if (!IPAddress.TryParse(gateway, out var gatewayIp))
        {
            return null;
        }

        var mac = ArpHelper.ResolveMacAddress(gatewayIp);
        var hostname = ArpHelper.ResolveHostname(gatewayIp);

        return new ConnectedDeviceInfo
        {
            Role = "Upstream device (gateway)",
            IpAddress = gateway,
            Hostname = hostname,
            MacAddress = mac,
            ExtraInfo = mac is null ? "ARP lookup pending — try Refresh" : null
        };
    }

    private static ConnectedDeviceInfo? GetWifiAccessPoint(string interfaceName, string gateway)
    {
        var wifiInfo = ParseWifiInterfaceInfo(interfaceName);
        if (wifiInfo is null)
        {
            return GetWiredUpstreamDevice(gateway);
        }

        return new ConnectedDeviceInfo
        {
            Role = "Access point",
            IpAddress = gateway is { Length: > 0 } ? gateway : null,
            Hostname = wifiInfo.Ssid,
            MacAddress = wifiInfo.Bssid,
            ExtraInfo = wifiInfo.Signal is not null ? $"SSID: {wifiInfo.Ssid} · Signal: {wifiInfo.Signal}" : $"SSID: {wifiInfo.Ssid}"
        };
    }

    private static WifiInterfaceInfo? ParseWifiInterfaceInfo(string interfaceName)
    {
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

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            return ParseWifiOutput(output, interfaceName);
        }
        catch
        {
            return null;
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
