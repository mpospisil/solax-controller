using Solax.Core.Enums;

namespace Solax.Core.Models;

public sealed record EnergyState(
    DateTimeOffset Timestamp,
    double BatterySocPercent,
    double BatteryPowerWatts,
    double SolarPowerWatts,
    double GridPowerWatts,
    EvChargerStatus EvChargerStatus,
    double EvChargerPowerWatts)
{
    /// <summary>
    /// Current solar production minus what's currently going into charging (EV + battery).
    /// Only the charging component of battery power counts here -- if the battery is
    /// discharging, that doesn't add back into this figure, since discharging isn't "charging".
    /// </summary>
    public double AvailableSolarPowerWatts =>
        SolarPowerWatts - EvChargerPowerWatts - Math.Max(BatteryPowerWatts, 0);

    /// <summary>
    /// Household consumption excluding the EV charger, the battery, and PV -- the "Other Loads"
    /// residual shown in the SolaX Cloud app. Derived from the same energy balance documented on
    /// <see cref="Strategies.SolarSurplusChargingStrategy"/> (positive Grid = importing, positive
    /// Battery = charging):
    /// <code>OtherLoads = PV + Grid - EV - Battery</code>
    /// </summary>
    public double OtherLoadsPowerWatts =>
        SolarPowerWatts + GridPowerWatts - EvChargerPowerWatts - BatteryPowerWatts;

    // Per the SolaX Gen4 protocol: battery/grid power registers are signed 16-bit
    // (negative = discharging/exporting); Powerdc1/2 (solar) and EV charge power are
    // unsigned (the HAC charger doesn't support V2G); SOC is an unsigned 0-100
    // percentage. Total solar power is the sum of both MPPT trackers (matches the
    // "Solar" figure shown in the SolaX Cloud app); total grid power is the sum of
    // the three phases (X3).
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
