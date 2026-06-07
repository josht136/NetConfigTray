using System.Net;
using System.Runtime.InteropServices;

namespace NetConfigTray.Helpers;

internal static class ArpHelper
{
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref int physicalAddrLen);

    public static string? ResolveMacAddress(IPAddress ipAddress)
    {
        try
        {
            var destIp = BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0);
            var macAddr = new byte[6];
            var macAddrLen = macAddr.Length;

            if (SendARP(destIp, 0, macAddr, ref macAddrLen) != 0)
            {
                return null;
            }

            return string.Join(":", macAddr.Take(macAddrLen).Select(b => b.ToString("X2")));
        }
        catch
        {
            return null;
        }
    }

    public static string? ResolveHostname(IPAddress ipAddress, TimeSpan timeout)
    {
        try
        {
            var task = Task.Run(() =>
            {
                var entry = Dns.GetHostEntry(ipAddress);
                return string.IsNullOrWhiteSpace(entry.HostName) ? null : entry.HostName;
            });

            return task.Wait(timeout) ? task.Result : null;
        }
        catch
        {
            return null;
        }
    }
}
