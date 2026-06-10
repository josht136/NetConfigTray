namespace NetConfigTray.Models;

/// <summary>
/// Live statistics for one ping target (a single destination, or a hop in an MTR-style trace).
/// Mutated in place by <see cref="Services.LatencyMonitorService"/> as samples arrive.
/// </summary>
public sealed class LatencyHop
{
    private const int MaxSamples = 120;
    private readonly List<double> _samples = new();
    private double? _previous;

    public int? HopNumber { get; init; }
    public string Address { get; set; } = "*";
    public string? Hostname { get; set; }

    public int Sent { get; private set; }
    public int Received { get; private set; }
    public double Last { get; private set; }
    public double Min { get; private set; } = double.MaxValue;
    public double Max { get; private set; }
    public double JitterSum { get; private set; }
    public int JitterCount { get; private set; }
    public double Total { get; private set; }

    public double Average => Received == 0 ? 0 : Total / Received;
    public double Jitter => JitterCount == 0 ? 0 : JitterSum / JitterCount;
    public double LossPercent => Sent == 0 ? 0 : (double)(Sent - Received) / Sent * 100.0;

    public IReadOnlyList<double> Samples => _samples;

    public void RecordSuccess(double rttMs)
    {
        Sent++;
        Received++;
        Last = rttMs;
        Total += rttMs;
        Min = Math.Min(Min, rttMs);
        Max = Math.Max(Max, rttMs);

        if (_previous is { } prev)
        {
            JitterSum += Math.Abs(rttMs - prev);
            JitterCount++;
        }

        _previous = rttMs;
        AddSample(rttMs);
    }

    public void RecordLoss()
    {
        Sent++;
        AddSample(0);
    }

    public double DisplayMin => Min == double.MaxValue ? 0 : Min;

    private void AddSample(double value)
    {
        _samples.Add(value);
        if (_samples.Count > MaxSamples)
        {
            _samples.RemoveAt(0);
        }
    }
}
