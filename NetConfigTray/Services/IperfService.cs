using System.Text.RegularExpressions;

namespace NetConfigTray.Services;

/// <summary>
/// Locates the bundled iperf3.exe and parses its console output. iperf3's <c>-J</c> mode only
/// emits a single JSON document at the end (not per-interval), so for a live chart we parse the
/// default human-readable interval lines, which are stable across versions.
/// </summary>
public static partial class IperfService
{
    public const string DownloadUrl = "https://iperf.fr/iperf-download.php";

    /// <summary>Resolves iperf3.exe from the bundled tools folder or PATH; null if unavailable.</summary>
    public static string? ResolveExecutable()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var bundled = Path.Combine(baseDir, "tools", "iperf3", "iperf3.exe");
            if (File.Exists(bundled))
            {
                return bundled;
            }

            var sideBySide = Path.Combine(baseDir, "iperf3.exe");
            if (File.Exists(sideBySide))
            {
                return sideBySide;
            }

            // Fall back to PATH.
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim(), "iperf3.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // Fall through to null.
        }

        return null;
    }

    public static bool IsAvailable => ResolveExecutable() is not null;

    public static string BuildClientArguments(
        string host,
        int port,
        int durationSeconds,
        int parallelStreams,
        bool reverse,
        bool udp,
        int udpBandwidthMbps)
    {
        var args = $"-c {host} -p {port} -t {durationSeconds} -P {parallelStreams} -i 1 -f m";
        if (reverse)
        {
            args += " -R";
        }

        if (udp)
        {
            args += $" -u -b {udpBandwidthMbps}M";
        }

        return args;
    }

    public static string BuildServerArguments(int port) => $"-s -p {port} -i 1 -f m";

    /// <summary>Extracts the Mbps value from an interval line, ignoring the SUM/header lines.</summary>
    public static bool TryParseIntervalMbps(string line, out double mbps)
    {
        mbps = 0;
        if (line.Contains("sender") || line.Contains("receiver"))
        {
            return false; // summary lines handled separately
        }

        var match = ThroughputRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        var value = double.Parse(match.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture);
        mbps = match.Groups["u"].Value switch
        {
            "G" => value * 1000,
            "K" => value / 1000,
            _ => value
        };
        return true;
    }

    public static bool IsSummaryLine(string line) =>
        (line.Contains("sender") || line.Contains("receiver")) && line.Contains("Bits/sec");

    [GeneratedRegex(@"(?<v>[0-9]+(?:\.[0-9]+)?)\s+(?<u>[GMK])bits/sec")]
    private static partial Regex ThroughputRegex();
}
