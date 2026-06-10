using System.Net;
using System.Text;
using NetConfigTray.Models;

namespace NetConfigTray.Helpers;

/// <summary>
/// Minimal Cisco Discovery Protocol parser. Expects the CDP payload (after the 802.3 LLC/SNAP
/// header). Extracts device id, port id, platform, software version, management IP, native VLAN.
/// </summary>
public static class CdpParser
{
    public static NeighborInfo? Parse(byte[] cdp)
    {
        // version(1) ttl(1) checksum(2) then TLVs.
        if (cdp.Length < 4)
        {
            return null;
        }

        string? deviceId = null, portId = null, platform = null, version = null, mgmt = null;
        int? vlan = null;
        var sawTlv = false;

        var index = 4;
        while (index + 4 <= cdp.Length)
        {
            var type = (cdp[index] << 8) | cdp[index + 1];
            var length = (cdp[index + 2] << 8) | cdp[index + 3];
            if (length < 4 || index + length > cdp.Length)
            {
                break;
            }

            var value = new ReadOnlySpan<byte>(cdp, index + 4, length - 4);
            sawTlv = true;

            switch (type)
            {
                case 0x0001:
                    deviceId = Ascii(value);
                    break;
                case 0x0002:
                    mgmt = ParseAddresses(value) ?? mgmt;
                    break;
                case 0x0003:
                    portId = Ascii(value);
                    break;
                case 0x0005:
                    version = Ascii(value);
                    break;
                case 0x0006:
                    platform = Ascii(value);
                    break;
                case 0x000A:
                    if (value.Length >= 2)
                    {
                        vlan = (value[0] << 8) | value[1];
                    }

                    break;
            }

            index += length;
        }

        if (!sawTlv)
        {
            return null;
        }

        return new NeighborInfo
        {
            Protocol = NeighborProtocol.Cdp,
            ChassisId = deviceId,
            SystemName = deviceId,
            PortId = portId,
            Platform = platform,
            SystemDescription = version,
            ManagementAddress = mgmt,
            Vlan = vlan
        };
    }

    private static string? ParseAddresses(ReadOnlySpan<byte> value)
    {
        if (value.Length < 4)
        {
            return null;
        }

        var count = (value[0] << 24) | (value[1] << 16) | (value[2] << 8) | value[3];
        var index = 4;
        for (var i = 0; i < count && index + 2 <= value.Length; i++)
        {
            var protoType = value[index];
            var protoLen = value[index + 1];
            index += 2 + protoLen;
            if (index + 2 > value.Length)
            {
                break;
            }

            var addrLen = (value[index] << 8) | value[index + 1];
            index += 2;
            if (index + addrLen > value.Length)
            {
                break;
            }

            // NLPID 0xCC = IPv4.
            if (protoType == 1 && protoLen == 1 && addrLen == 4)
            {
                try
                {
                    return new IPAddress(value.Slice(index, 4).ToArray()).ToString();
                }
                catch
                {
                    // Fall through.
                }
            }

            index += addrLen;
        }

        return null;
    }

    private static string? Ascii(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.ASCII.GetString(bytes).Trim('\0', ' ', '\r', '\n');
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
