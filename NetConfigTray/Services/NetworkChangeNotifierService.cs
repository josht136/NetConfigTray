using NetConfigTray.Models;

namespace NetConfigTray.Services;

public sealed class NetworkChangeNotifierService
{
    private readonly Dictionary<string, string> _previousSignatures = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public IReadOnlyList<string> DetectChanges(IReadOnlyList<NetworkInterfaceInfo> current)
    {
        if (!_initialized)
        {
            foreach (var info in current)
            {
                _previousSignatures[info.Id] = info.ChangeSignature;
            }

            _initialized = true;
            return Array.Empty<string>();
        }

        var messages = new List<string>();
        var currentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var info in current)
        {
            currentIds.Add(info.Id);
            var signature = info.ChangeSignature;

            if (!_previousSignatures.TryGetValue(info.Id, out var previous))
            {
                messages.Add($"{info.Name} connected ({info.IPv4Address})");
            }
            else if (!string.Equals(previous, signature, StringComparison.Ordinal))
            {
                messages.Add($"{info.Name} changed to {info.IPv4Address} ({info.ConfigurationLabel})");
            }

            _previousSignatures[info.Id] = signature;
        }

        foreach (var previousId in _previousSignatures.Keys.ToList())
        {
            if (!currentIds.Contains(previousId))
            {
                messages.Add("A network interface disconnected");
                _previousSignatures.Remove(previousId);
            }
        }

        return messages;
    }
}
