using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NetConfigTray.Services;

public sealed class WifiDetailsService
{
    public WifiDetails? GetDetails(string interfaceName)
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
            return ParseOutput(output, interfaceName);
        }
        catch
        {
            return null;
        }
    }

    private static WifiDetails? ParseOutput(string output, string interfaceName)
    {
        var blocks = Regex.Split(output, @"\r?\n\r?\n");
        foreach (var block in blocks)
        {
            var name = ReadValue(block, "Name");
            if (!string.Equals(name, interfaceName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var channelText = ReadValue(block, "Channel");
            var radioType = ReadValue(block, "Radio type");
            var band = ResolveBand(channelText, radioType);

            return new WifiDetails(
                channelText ?? "Unknown",
                band,
                radioType ?? "Unknown");
        }

        return null;
    }

    private static string ResolveBand(string? channelText, string? radioType)
    {
        if (int.TryParse(channelText, out var channel))
        {
            if (channel is >= 1 and <= 14)
            {
                return "2.4 GHz";
            }

            if (channel >= 36)
            {
                return "5 GHz";
            }
        }

        if (!string.IsNullOrWhiteSpace(radioType))
        {
            if (radioType.Contains("802.11ax", StringComparison.OrdinalIgnoreCase) ||
                radioType.Contains("802.11ac", StringComparison.OrdinalIgnoreCase))
            {
                return "5 GHz likely";
            }

            if (radioType.Contains("802.11n", StringComparison.OrdinalIgnoreCase) ||
                radioType.Contains("802.11g", StringComparison.OrdinalIgnoreCase) ||
                radioType.Contains("802.11b", StringComparison.OrdinalIgnoreCase))
            {
                return "2.4 GHz likely";
            }
        }

        return "Unknown";
    }

    private static string? ReadValue(string block, string key)
    {
        var match = Regex.Match(block, $@"^\s*{Regex.Escape(key)}\s*:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public readonly record struct WifiDetails(string Channel, string Band, string RadioType);
}
