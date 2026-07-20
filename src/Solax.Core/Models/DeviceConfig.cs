namespace Solax.Core.Models;

public sealed class DeviceConfig
{
    public required string Host { get; init; }

    public int Port { get; init; } = 502;

    public byte UnitId { get; init; } = 1;
}
