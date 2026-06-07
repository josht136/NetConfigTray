namespace NetConfigTray.Models;

public enum NeighborProtocol
{
    Lldp,
    Cdp
}

/// <summary>A discovered upstream switch/router neighbor (from an LLDP or CDP advertisement).</summary>
public sealed record NeighborInfo
{
    public required NeighborProtocol Protocol { get; init; }
    public string? ChassisId { get; init; }
    public string? PortId { get; init; }
    public string? PortDescription { get; init; }
    public string? SystemName { get; init; }
    public string? SystemDescription { get; init; }
    public string? ManagementAddress { get; init; }
    public string? Platform { get; init; }
    public int? Vlan { get; init; }
    public DateTime SeenUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Identity used to de-duplicate repeated advertisements from the same neighbor.</summary>
    public string Key => $"{Protocol}|{ChassisId}|{PortId}|{SystemName}";
}
