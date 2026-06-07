using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace NetConfigTray.Services;

public sealed class GatewayPingService
{
    private readonly ConcurrentDictionary<string, PingResult> _results = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string>? GatewayPingUpdated;

    public void QueuePing(string? gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return;
        }

        if (_results.TryGetValue(gateway, out var cached) &&
            (DateTime.UtcNow - cached.Timestamp).TotalSeconds < 5)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(gateway, 2000);
                var text = FormatLatency(reply.Status, reply.RoundtripTime);

                _results[gateway] = new PingResult(text, DateTime.UtcNow);
                GatewayPingUpdated?.Invoke(gateway);
            }
            catch
            {
                _results[gateway] = new PingResult("Unavailable", DateTime.UtcNow);
                GatewayPingUpdated?.Invoke(gateway);
            }
        });
    }

    public string GetLatencyText(string? gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return "No gateway";
        }

        if (_results.TryGetValue(gateway, out var result) &&
            (DateTime.UtcNow - result.Timestamp).TotalMinutes < 2)
        {
            return result.Text;
        }

        QueuePing(gateway);
        return "Pinging…";
    }

    private static string FormatLatency(IPStatus status, long roundtripTime)
    {
        if (status != IPStatus.Success)
        {
            return status switch
            {
                IPStatus.TimedOut => "Timed out",
                IPStatus.DestinationHostUnreachable => "Unreachable",
                IPStatus.DestinationNetworkUnreachable => "Network unreachable",
                _ => status.ToString()
            };
        }

        return roundtripTime <= 0 ? "<1 ms" : $"{roundtripTime} ms";
    }

    private readonly record struct PingResult(string Text, DateTime Timestamp);
}
