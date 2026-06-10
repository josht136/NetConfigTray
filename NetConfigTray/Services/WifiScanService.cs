using ManagedNativeWifi;

namespace NetConfigTray.Services;

/// <summary>A single visible BSSID (access point radio) from a Wi-Fi survey.</summary>
public sealed record WifiBss(
    string Ssid,
    string Bssid,
    int Rssi,
    int SignalPercent,
    int Channel,
    double BandGhz,
    string Phy,
    bool Secured);

/// <summary>
/// Wi-Fi survey via <c>ManagedNativeWifi</c> (wlanapi). Triggers a scan, enumerates visible BSSIDs,
/// and recommends the least-congested channel per band.
/// </summary>
public sealed class WifiScanService
{
    public async Task<IReadOnlyList<WifiBss>> ScanAsync()
    {
        try
        {
            await NativeWifi.ScanNetworksAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Scan trigger can fail if no Wi-Fi interface; enumeration below may still return cached data.
        }

        var secured = BuildSecurityMap();
        var result = new List<WifiBss>();

        try
        {
            foreach (var bss in NativeWifi.EnumerateBssNetworks())
            {
                var ssid = bss.Ssid.ToString();
                if (string.IsNullOrEmpty(ssid))
                {
                    ssid = "(hidden)";
                }

                var band = bss.Band > 0 ? Math.Round(bss.Band, 1) : BandFromFrequency(bss.Frequency);
                var channel = bss.Channel > 0 ? bss.Channel : ChannelFromFrequency(bss.Frequency);
                var percent = bss.LinkQuality is > 0 and <= 100 ? bss.LinkQuality : RssiToPercent(bss.Rssi);

                result.Add(new WifiBss(
                    ssid,
                    bss.Bssid?.ToString() ?? "—",
                    bss.Rssi,
                    percent,
                    channel,
                    band,
                    bss.PhyType.ToString(),
                    secured.TryGetValue(ssid, out var isSecure) && isSecure));
            }
        }
        catch
        {
            // No interface or enumeration failed.
        }

        return result
            .OrderByDescending(b => b.Rssi)
            .ToList();
    }

    private static Dictionary<string, bool> BuildSecurityMap()
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        try
        {
            foreach (var net in NativeWifi.EnumerateAvailableNetworks())
            {
                var ssid = net.Ssid.ToString();
                if (!string.IsNullOrEmpty(ssid))
                {
                    map[ssid] = net.IsSecurityEnabled;
                }
            }
        }
        catch
        {
            // Ignore.
        }

        return map;
    }

    /// <summary>
    /// Returns the least-congested channel among the standard non-overlapping channels for the band,
    /// scoring channels by the signal strength of nearby/overlapping networks.
    /// </summary>
    public static int RecommendChannel(IReadOnlyList<WifiBss> networks, double bandGhz)
    {
        var candidates = bandGhz switch
        {
            2.4 => new[] { 1, 6, 11 },
            5.0 => new[] { 36, 40, 44, 48, 149, 153, 157, 161 },
            _ => new[] { 37, 53, 69, 85 }
        };

        var bandNetworks = networks.Where(n => Math.Abs(n.BandGhz - bandGhz) < 0.6).ToList();
        if (bandNetworks.Count == 0)
        {
            return candidates[0];
        }

        var best = candidates[0];
        var bestScore = double.MaxValue;
        foreach (var candidate in candidates)
        {
            double score = 0;
            foreach (var net in bandNetworks)
            {
                var distance = Math.Abs(net.Channel - candidate);
                // 2.4 GHz channels overlap within ~4; 5/6 GHz are effectively non-overlapping per 20 MHz.
                var overlap = bandGhz == 2.4
                    ? Math.Max(0, 5 - distance) / 5.0
                    : (distance == 0 ? 1.0 : 0.0);

                if (overlap > 0)
                {
                    score += overlap * (net.SignalPercent + 1);
                }
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private static double BandFromFrequency(int frequencyKhz)
    {
        var mhz = frequencyKhz / 1000.0;
        return mhz switch
        {
            >= 2400 and < 2500 => 2.4,
            >= 4900 and < 5925 => 5.0,
            >= 5925 and <= 7125 => 6.0,
            _ => 0.0
        };
    }

    private static int ChannelFromFrequency(int frequencyKhz)
    {
        var mhz = (int)(frequencyKhz / 1000.0);
        if (mhz == 2484)
        {
            return 14;
        }

        if (mhz is >= 2412 and <= 2472)
        {
            return (mhz - 2407) / 5;
        }

        if (mhz is >= 5000 and <= 5895)
        {
            return (mhz - 5000) / 5;
        }

        if (mhz is >= 5955 and <= 7115)
        {
            return (mhz - 5950) / 5;
        }

        return 0;
    }

    private static int RssiToPercent(int rssiDbm)
    {
        // Typical usable range -90 dBm (0%) to -30 dBm (100%).
        var percent = 2 * (rssiDbm + 100);
        return Math.Clamp(percent, 0, 100);
    }
}
