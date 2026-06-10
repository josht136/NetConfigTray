namespace NetConfigTray.Models;

public enum ConsoleTransportKind
{
    Serial,
    Ssh,
    Telnet
}

/// <summary>
/// A saved console connection profile (no passwords are persisted). Stored as a JSON list via
/// <see cref="Services.SettingsStore"/> under the "console-sessions" section.
/// </summary>
public sealed class ConsoleSessionProfile
{
    public string Name { get; set; } = "New session";
    public ConsoleTransportKind Kind { get; set; } = ConsoleTransportKind.Serial;

    // Serial
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";

    // SSH / Telnet
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;

    public override string ToString() => Name;
}

public sealed class ConsoleSessionList
{
    public List<ConsoleSessionProfile> Sessions { get; set; } = new();
}
