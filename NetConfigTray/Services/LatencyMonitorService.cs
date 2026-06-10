using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using NetConfigTray.Models;

namespace NetConfigTray.Services;

/// <summary>
/// Continuous latency/jitter/loss monitor. In single-target mode it pings one host; in MTR mode it
/// first traces the route (TTL-limited pings) then pings each discovered hop every interval.
/// All work runs on a background loop; <see cref="Updated"/> fires after each cycle.
/// </summary>
public sealed class LatencyMonitorService : IDisposable
{
    private const int MaxHops = 30;
    private const int PingTimeoutMs = 1500;
    private const int TraceTimeoutMs = 2000;

    private readonly List<LatencyHop> _hops = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event Action? Updated;
    public event Action<string>? Failed;

    public int IntervalMs { get; set; } = 1000;
    public string Target { get; private set; } = string.Empty;
    public bool IsMtr { get; private set; }

    public IReadOnlyList<LatencyHop> Snapshot()
    {
        lock (_lock)
        {
            return _hops.ToList();
        }
    }

    public void Start(string target, bool mtr)
    {
        Stop();
        Target = target;
        IsMtr = mtr;

        lock (_lock)
        {
            _hops.Clear();
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => RunAsync(target, mtr, token), token);
    }

    private async Task RunAsync(string target, bool mtr, CancellationToken token)
    {
        IPAddress? destination;
        try
        {
            destination = await ResolveAsync(target);
            if (destination is null)
            {
                Failed?.Invoke($"Could not resolve {target}.");
                return;
            }
        }
        catch (Exception ex)
        {
            Failed?.Invoke($"Resolve failed: {ex.Message}");
            return;
        }

        if (mtr)
        {
            await DiscoverHopsAsync(destination, token);
        }
        else
        {
            lock (_lock)
            {
                _hops.Add(new LatencyHop { Address = destination.ToString(), Hostname = target });
            }
        }

        Updated?.Invoke();

        while (!token.IsCancellationRequested)
        {
            await PingAllHopsAsync(token);
            Updated?.Invoke();

            try
            {
                await Task.Delay(IntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task DiscoverHopsAsync(IPAddress destination, CancellationToken token)
    {
        using var ping = new Ping();
        var buffer = Encoding.ASCII.GetBytes("TNT-latency-probe");

        for (var ttl = 1; ttl <= MaxHops && !token.IsCancellationRequested; ttl++)
        {
            try
            {
                var options = new PingOptions(ttl, true);
                var reply = await ping.SendPingAsync(destination, TraceTimeoutMs, buffer, options);

                var hop = new LatencyHop
                {
                    HopNumber = ttl,
                    Address = reply.Address?.ToString() is { Length: > 0 } addr && addr != "0.0.0.0" ? addr : "*"
                };

                lock (_lock)
                {
                    _hops.Add(hop);
                }

                Updated?.Invoke();

                if (reply.Status == IPStatus.Success)
                {
                    break;
                }
            }
            catch
            {
                lock (_lock)
                {
                    _hops.Add(new LatencyHop { HopNumber = ttl, Address = "*" });
                }
            }
        }

        _ = ResolveHopNamesAsync();
    }

    private async Task ResolveHopNamesAsync()
    {
        var hops = Snapshot();
        foreach (var hop in hops)
        {
            if (hop.Address == "*" || !IPAddress.TryParse(hop.Address, out var ip))
            {
                continue;
            }

            try
            {
                var entry = await Dns.GetHostEntryAsync(ip);
                hop.Hostname = entry.HostName;
                Updated?.Invoke();
            }
            catch
            {
                // No reverse DNS.
            }
        }
    }

    private async Task PingAllHopsAsync(CancellationToken token)
    {
        var hops = Snapshot();
        foreach (var hop in hops)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (hop.Address == "*")
            {
                hop.RecordLoss();
                continue;
            }

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(hop.Address, PingTimeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    hop.RecordSuccess(reply.RoundtripTime);
                }
                else
                {
                    hop.RecordLoss();
                }
            }
            catch
            {
                hop.RecordLoss();
            }
        }
    }

    private static async Task<IPAddress?> ResolveAsync(string target)
    {
        if (IPAddress.TryParse(target, out var ip))
        {
            return ip;
        }

        var entry = await Dns.GetHostAddressesAsync(target);
        return entry.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
               ?? entry.FirstOrDefault();
    }

    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hop,Address,Hostname,Sent,Received,Loss%,Last(ms),Min(ms),Avg(ms),Max(ms),Jitter(ms)");
        foreach (var hop in Snapshot())
        {
            sb.AppendLine(string.Join(",",
                hop.HopNumber?.ToString() ?? "-",
                hop.Address,
                hop.Hostname ?? string.Empty,
                hop.Sent,
                hop.Received,
                hop.LossPercent.ToString("0.0"),
                hop.Last.ToString("0.0"),
                hop.DisplayMin.ToString("0.0"),
                hop.Average.ToString("0.0"),
                hop.Max.ToString("0.0"),
                hop.Jitter.ToString("0.0")));
        }

        return sb.ToString();
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // Ignore.
        }

        _cts = null;
        _loop = null;
    }

    public void Dispose() => Stop();
}
