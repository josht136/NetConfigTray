using System.Text.Json;

namespace NetConfigTray.Services;

/// <summary>
/// Lightweight JSON-backed settings/session store. Each named section is persisted as its own
/// file under %APPDATA%\TNT\. All operations are best-effort and never throw.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _root;
    private readonly object _lock = new();

    public SettingsStore()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TNT");
    }

    public string RootDirectory => _root;

    public T Load<T>(string name) where T : new()
    {
        try
        {
            var path = PathFor(name);
            if (!File.Exists(path))
            {
                return new T();
            }

            lock (_lock)
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
            }
        }
        catch
        {
            return new T();
        }
    }

    public void Save<T>(string name, T value)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(_root);
                var json = JsonSerializer.Serialize(value, Options);
                File.WriteAllText(PathFor(name), json);
            }
        }
        catch
        {
            // Best effort; settings are non-critical.
        }
    }

    private string PathFor(string name) => Path.Combine(_root, $"{name}.json");
}
