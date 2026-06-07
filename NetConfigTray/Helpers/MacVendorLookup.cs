using System.Collections.Concurrent;
using System.Net.Http;

namespace NetConfigTray.Helpers;

/// <summary>
/// Resolves the manufacturer (vendor) of a network device from its MAC address OUI.
/// Results are cached per-OUI so the network is hit at most once per distinct device.
/// Uses a small offline table for common vendors and falls back to the free
/// macvendors.com API. All calls are blocking and intended to run off the UI thread.
/// </summary>
internal static class MacVendorLookup
{
    private static readonly ConcurrentDictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    // Minimal offline fallback for very common consumer networking OUIs.
    private static readonly Dictionary<string, string> OfflineOuis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["F0:9F:C2"] = "Ubiquiti",
        ["FC:EC:DA"] = "Ubiquiti",
        ["B4:FB:E4"] = "Ubiquiti",
        ["00:1A:11"] = "Google",
        ["00:0C:29"] = "VMware",
        ["00:50:56"] = "VMware",
        ["00:15:5D"] = "Microsoft (Hyper-V)",
        ["DC:A6:32"] = "Raspberry Pi",
        ["B8:27:EB"] = "Raspberry Pi",
        ["00:05:5D"] = "D-Link",
        ["00:1D:7E"] = "Cisco-Linksys",
        ["C0:56:27"] = "Belkin",
        ["00:18:E7"] = "Netgear",
        ["A0:40:A0"] = "Netgear",
        ["00:1F:33"] = "Netgear",
        ["E8:DE:27"] = "TP-Link",
        ["50:C7:BF"] = "TP-Link",
        ["00:90:4C"] = "Epigram/Broadcom",
        ["00:14:BF"] = "Cisco-Linksys"
    };

    public static string? Resolve(string? macAddress)
    {
        var oui = NormalizeOui(macAddress);
        if (oui is null)
        {
            return null;
        }

        if (Cache.TryGetValue(oui, out var cached))
        {
            return cached;
        }

        if (OfflineOuis.TryGetValue(oui, out var offline))
        {
            Cache[oui] = offline;
            return offline;
        }

        var resolved = QueryApi(macAddress!);

        // Cache successful lookups and definitive "unknown" results, but not transient
        // failures (timeouts / rate limiting), so they can be retried on a later refresh.
        if (resolved is not null)
        {
            Cache[oui] = resolved.Length == 0 ? null : resolved;
        }

        return resolved is { Length: > 0 } ? resolved : null;
    }

    /// <summary>
    /// Returns the vendor string, an empty string for a definitive "unknown" (HTTP 404),
    /// or null for a transient failure that should not be cached.
    /// </summary>
    private static string? QueryApi(string macAddress)
    {
        try
        {
            using var response = Http.GetAsync($"https://api.macvendors.com/{Uri.EscapeDataString(macAddress)}")
                .GetAwaiter()
                .GetResult();

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return string.Empty;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim();
            return string.IsNullOrWhiteSpace(body) ? string.Empty : body;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeOui(string? macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return null;
        }

        var hex = new string(macAddress.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length < 6)
        {
            return null;
        }

        var prefix = hex[..6].ToUpperInvariant();
        return $"{prefix[..2]}:{prefix[2..4]}:{prefix[4..6]}";
    }
}
