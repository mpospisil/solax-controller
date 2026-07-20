using Solax.Core.Models;

namespace Solax.Worker.Configuration;

public sealed class SolaxOptions
{
    public const string SectionName = "Solax";

    public required DeviceConfig Inverter { get; init; }

    public required DeviceConfig EvCharger { get; init; }

    public int PollIntervalSeconds { get; init; } = 5;

    public ChargingStrategyOptions ChargingStrategy { get; init; } = new();
}

public sealed class ChargingStrategyOptions
{
    public double NominalVoltage { get; init; } = 230;

    public double MinChargingCurrentAmps { get; init; } = 6;

    public double MaxChargingCurrentAmps { get; init; } = 20;
}
