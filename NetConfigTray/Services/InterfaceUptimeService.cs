namespace NetConfigTray.Services;

public sealed class InterfaceUptimeService
{
    private readonly Dictionary<string, DateTime> _firstSeen = new(StringComparer.OrdinalIgnoreCase);

    public string GetUptimeText(string interfaceId, bool isActive)
    {
        if (!isActive)
        {
            _firstSeen.Remove(interfaceId);
            return "Disconnected";
        }

        var now = DateTime.UtcNow;
        if (!_firstSeen.TryGetValue(interfaceId, out var firstSeen))
        {
            _firstSeen[interfaceId] = now;
            return "Just connected";
        }

        return FormatDuration(now - firstSeen);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
        {
            return "Just connected";
        }

        if (duration.TotalHours < 1)
        {
            return $"{(int)duration.TotalMinutes} min";
        }

        if (duration.TotalDays < 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        return $"{(int)duration.TotalDays}d {duration.Hours}h";
    }
}
