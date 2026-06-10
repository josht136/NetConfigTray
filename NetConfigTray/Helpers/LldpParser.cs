using System.Net;
using System.Text;
using NetConfigTray.Models;

namespace NetConfigTray.Helpers;

/// <summary>
/// Minimal LLDP (IEEE 802.1AB) parser. Walks the TLV stream of an LLDPDU and extracts the fields
/// a field engineer cares about: chassis/port id, system name/description, management IP, VLAN.
/// </summary>
public static class LldpParser
{
    public static NeighborInfo? Parse(byte[] payload)
    {
        if (payload.Length < 2)
        {
            return null;
        }

        string? chassisId = null, portId = null, portDesc = null, sysName = null, sysDesc = null, mgmt = null;
        int? vlan = null;
        var sawTlv = false;

        var index = 0;
        while (index + 2 <= payload.Length)
        {
            var header = (payload[index] << 8) | payload[index + 1];
            var type = header >> 9;
            var length = header & 0x1FF;
            index += 2;

            if (type == 0 || index + length > payload.Length)
            {
                break;
            }

            var value = new ReadOnlySpan<byte>(payload, index, length);
            sawTlv = true;

            switch (type)
            {
                case 1:
                    chassisId = ParseIdField(value);
                    break;
                case 2:
                    portId = ParseIdField(value);
                    break;
                case 4:
                    portDesc = AsciiOrNull(value);
                    break;
                case 5:
                    sysName = AsciiOrNull(value);
                    break;
                case 6:
                    sysDesc = AsciiOrNull(value);
                    break;
                case 8:
                    mgmt = ParseManagementAddress(value) ?? mgmt;
                    break;
                case 127:
                    ParseOrgSpecific(value, ref vlan);
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
            Protocol = NeighborProtocol.Lldp,
            ChassisId = chassisId,
            PortId = portId,
            PortDescription = portDesc,
            SystemName = sysName,
            SystemDescription = sysDesc,
            ManagementAddress = mgmt,
            Vlan = vlan
        };
    }

    private static string? ParseIdField(ReadOnlySpan<byte> value)
    {
        if (value.Length < 2)
        {
            return null;
        }

        var subtype = value[0];
        var data = value[1..];

        // MAC-address subtypes (chassis 4, port 3) -> format as colon hex.
        if ((subtype == 4 && data.Length == 6) || (subtype == 3 && data.Length == 6))
        {
            return FormatMac(data);
        }

        return AsciiOrHex(data);
    }

    private static string? ParseManagementAddress(ReadOnlySpan<byte> value)
    {
        if (value.Length < 2)
        {
            return null;
        }

        var addrLen = value[0];
        if (addrLen < 1 || 1 + addrLen > value.Length)
        {
            return null;
        }

        var subtype = value[1];
        var addr = value.Slice(2, addrLen - 1);
        try
        {
            return subtype switch
            {
                1 when addr.Length == 4 => new IPAddress(addr.ToArray()).ToString(),
                2 when addr.Length == 16 => new IPAddress(addr.ToArray()).ToString(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static void ParseOrgSpecific(ReadOnlySpan<byte> value, ref int? vlan)
    {
        if (value.Length < 4)
        {
            return;
        }

        // IEEE 802.1 OUI 00-80-C2, subtype 1 = Port VLAN ID (2 bytes).
        var isDot1 = value[0] == 0x00 && value[1] == 0x80 && value[2] == 0xC2;
        var subtype = value[3];
        if (isDot1 && subtype == 1 && value.Length >= 6)
        {
            vlan = (value[4] << 8) | value[5];
        }
    }

    private static string FormatMac(ReadOnlySpan<byte> bytes)
    {
        var parts = new string[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            parts[i] = bytes[i].ToString("X2");
        }

        return string.Join(":", parts);
    }

    private static string? AsciiOrNull(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.ASCII.GetString(bytes).Trim('\0', ' ', '\r', '\n');
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string AsciiOrHex(ReadOnlySpan<byte> bytes)
    {
        var printable = true;
        foreach (var b in bytes)
        {
            if (b is < 0x20 or > 0x7E)
            {
                printable = false;
                break;
            }
        }

        if (printable && bytes.Length > 0)
        {
            return Encoding.ASCII.GetString(bytes);
        }

        return FormatMac(bytes);
    }
}
