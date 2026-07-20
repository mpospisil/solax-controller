using Solax.Core.Models;

namespace Solax.Worker.Configuration;

public sealed class SolaxOptions
{
    public const string SectionName = "Solax";

    public required DeviceConfig Inverter { get; init; }

    public required DeviceConfig EvCharger { get; init; }

    public int PollIntervalSeconds { get; init; } = 5;
}
