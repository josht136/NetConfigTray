using System.Net.Sockets;
using NetConfigTray.Helpers;

namespace NetConfigTray.Services;

/// <summary>
/// Parallel TCP connect scanner (Nmap-style, no native deps). Attempts a connection to each port
/// with a timeout and optionally grabs a short banner from open ports.
/// </summary>
public sealed class PortScanService
{
    private const int Concurrency = 100;
    private const int ConnectTimeoutMs = 700;
    private const int BannerTimeoutMs = 500;

    public sealed record OpenPort(int Port, string Service, string? Banner);

    public async Task ScanAsync(
        string host,
        IReadOnlyList<int> ports,
        bool grabBanner,
        Action<OpenPort> onOpen,
        Action onProgress,
        CancellationToken token)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Concurrency,
            CancellationToken = token
        };

        await Parallel.ForEachAsync(ports, options, async (port, ct) =>
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port, ct).AsTask();
                var completed = await Task.WhenAny(connectTask, Task.Delay(ConnectTimeoutMs, ct));

                if (completed == connectTask && client.Connected)
                {
                    string? banner = null;
                    if (grabBanner)
                    {
                        banner = await TryGrabBanner(client, ct);
                    }

                    onOpen(new OpenPort(port, WellKnownPorts.ServiceName(port), banner));
                }
            }
            catch
            {
                // Closed / filtered / cancelled.
            }
            finally
            {
                onProgress();
            }
        });
    }

    private static async Task<string?> TryGrabBanner(TcpClient client, CancellationToken token)
    {
        try
        {
            var stream = client.GetStream();
            var buffer = new byte[256];
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(BannerTimeoutMs);

            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token);
            if (read <= 0)
            {
                return null;
            }

            var text = System.Text.Encoding.ASCII.GetString(buffer, 0, read)
                .Replace("\r", " ").Replace("\n", " ").Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses a spec like "22,80,443,1000-1024" into a sorted, de-duplicated port list.</summary>
    public static List<int> ParsePorts(string spec)
    {
        var ports = new SortedSet<int>();
        foreach (var token in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Contains('-'))
            {
                var bounds = token.Split('-', StringSplitOptions.TrimEntries);
                if (bounds.Length == 2 &&
                    int.TryParse(bounds[0], out var start) &&
                    int.TryParse(bounds[1], out var end))
                {
                    if (start > end)
                    {
                        (start, end) = (end, start);
                    }

                    for (var p = Math.Max(1, start); p <= Math.Min(65535, end); p++)
                    {
                        ports.Add(p);
                    }
                }
            }
            else if (int.TryParse(token, out var port) && port is >= 1 and <= 65535)
            {
                ports.Add(port);
            }
        }

        return ports.ToList();
    }
}
