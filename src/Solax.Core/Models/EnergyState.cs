using Solax.Core.Enums;

namespace Solax.Core.Models;

public sealed record EnergyState(
    DateTimeOffset Timestamp,
    double BatterySocPercent,
    double BatteryPowerWatts,
    double SolarPowerWatts,
    double GridPowerWatts,
    EvChargerStatus EvChargerStatus,
    double EvChargerPowerWatts,
    // The charger's work/use mode (Fast/ECO/Green/Stop), or null when it couldn't be read (the
    // holding register isn't available on every charger/firmware). Doesn't come from the inverter
    // telemetry block, so it's attached after FromRawRegisters rather than being a raw parameter.
    EvChargerMode? ChargeMode = null,
    // The charger's active current setpoint in amps, or null when it couldn't be read. Same story as
    // ChargeMode: a control holding register, attached after FromRawRegisters.
    int? ChargeCurrentAmps = null)
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

    /// <summary>
    /// Solar power the EV may draw without importing from the grid or discharging the battery.
    ///
    /// Deliberately derived only from PV, battery and EV power — never from the grid register, which
    /// on this hardware reports the inverter's AC output rather than the meter and cannot tell us the
    /// household load. Two rules, both directly enforcing "solar only":
    ///
    /// 1. It can never exceed what the panels are producing, minus whatever the battery is taking:
    ///    <c>Solar - max(Battery, 0)</c>. You cannot give the car power that doesn't exist.
    /// 2. If the battery is <em>discharging</em>, total demand already exceeds supply, so the car must
    ///    give back exactly that deficit: <c>EV + Battery</c> (Battery being negative here).
    ///
    /// Taking the lower of the two makes the loop self-correcting: any battery discharge caused by the
    /// car pulls the figure straight back down on the next poll.
    /// </summary>
    public double SolarSurplusPowerWatts
    {
        get
        {
            var availableFromPanels = SolarPowerWatts - Math.Max(BatteryPowerWatts, 0);

            return BatteryPowerWatts < 0
                ? Math.Min(availableFromPanels, EvChargerPowerWatts + BatteryPowerWatts)
                : availableFromPanels;
        }
    }

    // Per the SolaX Gen4 protocol: the battery power register is signed 16-bit with
    // positive = charging (negative = discharging). The per-phase grid/feed-in registers are
    // also signed, but use SolaX's meter convention where positive = EXPORT (feed-in to grid);
    // we negate their sum so this model's convention is positive Grid = importing, matching the
    // charging strategies and OtherLoadsPowerWatts. Powerdc1/2 (solar) and EV charge power are
    // unsigned (the HAC charger doesn't support V2G); SOC is an unsigned 0-100 percentage. Total
    // solar power is the sum of both MPPT trackers (matches the "Solar" figure in the SolaX Cloud
    // app); total grid power is the sum of the three phases (X3).
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
            // Negate: SolaX reports grid power as positive = export, but this model treats positive = import.
            -(unchecked((short)gridPowerRRaw) + unchecked((short)gridPowerSRaw) + unchecked((short)gridPowerTRaw)),
            EvChargerStatusMapping.FromRaw(evChargerStatusRaw),
            evChargerPowerRaw);
    }
}
