using System.Collections.Concurrent;
using System.Net.NetworkInformation;

namespace NetConfigTray.Services;

public sealed class GatewayPingService
{
    private readonly ConcurrentDictionary<string, PingResult> _results = new(StringComparer.OrdinalIgnoreCase);

    public void QueuePing(string? gateway)
    {
        if (string.IsNullOrWhiteSpace(gateway))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(gateway, 2000);
                var text = reply.Status == IPStatus.Success
                    ? $"{reply.RoundtripTime} ms"
                    : reply.Status.ToString();

                _results[gateway] = new PingResult(text, DateTime.UtcNow);
            }
            catch
            {
                _results[gateway] = new PingResult("Unavailable", DateTime.UtcNow);
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

    private readonly record struct PingResult(string Text, DateTime Timestamp);
}
