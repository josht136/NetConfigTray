namespace NetConfigTray.Services.Console;

/// <summary>
/// A bidirectional byte/character stream to a device console (serial cable, SSH shell, or Telnet).
/// Implementations raise <see cref="DataReceived"/> on a background thread; the form marshals to UI.
/// </summary>
public interface IConsoleTransport : IDisposable
{
    /// <summary>Raised with decoded text as it arrives from the device.</summary>
    event Action<string>? DataReceived;

    /// <summary>Raised when the connection drops or closes, with a human-readable reason.</summary>
    event Action<string>? Disconnected;

    bool IsConnected { get; }

    /// <summary>Short human-readable description, e.g. "COM3 @ 9600 8N1" or "ssh admin@10.0.0.1".</summary>
    string Description { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Sends raw text (typically user keystrokes) to the device.</summary>
    void Send(string text);

    void Disconnect();
}
