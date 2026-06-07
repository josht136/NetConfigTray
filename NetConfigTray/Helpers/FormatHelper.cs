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
            return "0 B/s";
        }

        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        var value = (double)bytesPerSecond;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
