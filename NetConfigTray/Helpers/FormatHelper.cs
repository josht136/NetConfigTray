using System.Net.NetworkInformation;

namespace NetConfigTray.Helpers;

public static class FormatHelper
{
    public static string FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length == 0)
        {
            return "Unknown";
        }

        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    public static string FormatLinkSpeed(long speedBps)
    {
        if (speedBps <= 0)
        {
            return "Unknown";
        }

        if (speedBps >= 1_000_000_000)
        {
            return $"{speedBps / 1_000_000_000.0:0.#} Gbps";
        }

        if (speedBps >= 1_000_000)
        {
            return $"{speedBps / 1_000_000.0:0.#} Mbps";
        }

        return $"{speedBps / 1_000.0:0.#} Kbps";
    }

    public static string FormatThroughput(long bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
        {
            return "0 bps";
        }

        // Network throughput is conventionally expressed in bits/second.
        string[] units = ["bps", "Kbps", "Mbps", "Gbps"];
        var value = bytesPerSecond * 8.0;
        var unit = 0;

        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
