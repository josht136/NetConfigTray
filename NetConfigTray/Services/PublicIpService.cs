namespace NetConfigTray.Services;

public sealed class PublicIpService : IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly object _lock = new();
    private string? _cachedIp;
    private DateTime _cachedAt;
    private bool _isRefreshing;

    public string GetDisplayText()
    {
        lock (_lock)
        {
            if (_cachedIp is not null && (DateTime.UtcNow - _cachedAt).TotalMinutes < 10)
            {
                return _cachedIp;
            }
        }

        RefreshAsync();
        return _cachedIp ?? "Loading…";
    }

    public void RefreshAsync()
    {
        lock (_lock)
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var ip = await _httpClient.GetStringAsync("https://api.ipify.org");
                ip = ip.Trim();

                lock (_lock)
                {
                    _cachedIp = string.IsNullOrWhiteSpace(ip) ? "Unavailable" : ip;
                    _cachedAt = DateTime.UtcNow;
                    _isRefreshing = false;
                }
            }
            catch
            {
                lock (_lock)
                {
                    _cachedIp = "Unavailable";
                    _cachedAt = DateTime.UtcNow;
                    _isRefreshing = false;
                }
            }
        });
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
