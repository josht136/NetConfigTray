namespace NetConfigTray.Services;

public sealed class ThroughputHistoryService
{
    private const int MaxSamples = 30;
    private readonly Dictionary<string, List<long>> _downloadHistory = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<long>> _uploadHistory = new(StringComparer.OrdinalIgnoreCase);

    public void AddSample(string interfaceId, long downloadBps, long uploadBps)
    {
        AddToHistory(_downloadHistory, interfaceId, downloadBps);
        AddToHistory(_uploadHistory, interfaceId, uploadBps);
    }

    public IReadOnlyList<long> GetDownloadHistory(string interfaceId)
    {
        return _downloadHistory.TryGetValue(interfaceId, out var history)
            ? history
            : Array.Empty<long>();
    }

    public IReadOnlyList<long> GetUploadHistory(string interfaceId)
    {
        return _uploadHistory.TryGetValue(interfaceId, out var history)
            ? history
            : Array.Empty<long>();
    }

    private static void AddToHistory(Dictionary<string, List<long>> store, string interfaceId, long value)
    {
        if (!store.TryGetValue(interfaceId, out var history))
        {
            history = new List<long>(MaxSamples);
            store[interfaceId] = history;
        }

        history.Add(value);
        if (history.Count > MaxSamples)
        {
            history.RemoveAt(0);
        }
    }
}
