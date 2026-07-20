using Solax.Core.Enums;

namespace Solax.Core.Models;

public sealed record EnergyState(
    DateTimeOffset Timestamp,
    double BatterySocPercent,
    double BatteryPowerWatts,
    double PvPowerWatts,
    double GridPowerWatts,
    EvChargerStatus EvChargerStatus,
    double EvChargerPowerWatts)
{
    // Power registers are signed 16-bit (negative = discharging/exporting) per the
    // SolaX Gen4 protocol convention; SOC is an unsigned 0-100 percentage.
    public static EnergyState FromRawRegisters(
        DateTimeOffset timestamp,
        ushort batterySocRaw,
        ushort batteryPowerRaw,
        ushort pvPowerRaw,
        ushort gridPowerRaw,
        ushort evChargerStatusRaw,
        ushort evChargerPowerRaw)
    {
        return new EnergyState(
            timestamp,
            batterySocRaw,
            unchecked((short)batteryPowerRaw),
            unchecked((short)pvPowerRaw),
            unchecked((short)gridPowerRaw),
            EvChargerStatusMapping.FromRaw(evChargerStatusRaw),
            unchecked((short)evChargerPowerRaw));
    }
}
