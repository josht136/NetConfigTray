namespace NetConfigTray.Models;

public sealed class ConnectedDeviceInfo
{
    public required string Role { get; init; }
    public string? IpAddress { get; init; }
    public string? Hostname { get; init; }
    public string? MacAddress { get; init; }
    public string? ExtraInfo { get; init; }
}
