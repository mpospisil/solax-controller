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
    // Per the SolaX Gen4 protocol: battery/grid power registers are signed 16-bit
    // (negative = discharging/exporting); Powerdc1/2 (PV) and EV charge power are
    // unsigned (the HAC charger doesn't support V2G); SOC is an unsigned 0-100
    // percentage. Total PV power is the sum of both MPPT trackers; total grid power
    // is the sum of the three phases (X3).
    public static EnergyState FromRawRegisters(
        DateTimeOffset timestamp,
        ushort batterySocRaw,
        ushort batteryPowerRaw,
        ushort pvPowerDc1Raw,
        ushort pvPowerDc2Raw,
        ushort gridPowerRRaw,
        ushort gridPowerSRaw,
        ushort gridPowerTRaw,
        ushort evChargerStatusRaw,
        ushort evChargerPowerRaw)
    {
        return new EnergyState(
            timestamp,
            batterySocRaw,
            unchecked((short)batteryPowerRaw),
            pvPowerDc1Raw + pvPowerDc2Raw,
            unchecked((short)gridPowerRRaw) + unchecked((short)gridPowerSRaw) + unchecked((short)gridPowerTRaw),
            EvChargerStatusMapping.FromRaw(evChargerStatusRaw),
            evChargerPowerRaw);
    }
}
