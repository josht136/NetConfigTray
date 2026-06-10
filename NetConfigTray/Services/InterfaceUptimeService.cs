namespace NetConfigTray.Services;

/// <summary>
/// Estimates how long an interface's link has actually been up, rather than how long
/// the app has been running.
///
/// Windows does not expose a reliable per-interface "link up since" timestamp without
/// kernel tracing, so this uses a pragmatic heuristic:
///   - Interfaces that are already active when the app starts are assumed to have been
///     up since system boot (true for the common always-on Ethernet/Wi-Fi case).
///   - Interfaces that first appear active later in the session are timed from when they
///     were first observed (their real connect moment).
///   - When an interface goes down and comes back, its timer restarts on reconnect.
/// </summary>
public sealed class InterfaceUptimeService
{
    private static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(15);

    private readonly Dictionary<string, DateTime> _firstSeenActive = new(StringComparer.OrdinalIgnoreCase);
    private readonly DateTime _appStartUtc = DateTime.UtcNow;
    private readonly DateTime _bootTimeUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);

    public string GetUptimeText(string interfaceId, bool isActive)
    {
        if (!isActive)
        {
            _firstSeenActive.Remove(interfaceId);
            return "Disconnected";
        }

        var now = DateTime.UtcNow;
        if (!_firstSeenActive.TryGetValue(interfaceId, out var firstSeen))
        {
            firstSeen = now;
            _firstSeenActive[interfaceId] = firstSeen;
        }

        // If the interface was already active at (or right after) app startup, treat it as
        // having been up since boot. Otherwise it genuinely connected during the session.
        var connectedSince = firstSeen - _appStartUtc <= StartupGrace
            ? _bootTimeUtc
            : firstSeen;

        return FormatDuration(now - connectedSince);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration.TotalMinutes < 1)
        {
            return "Less than a minute";
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
