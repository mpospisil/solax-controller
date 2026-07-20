using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Core.Strategies;

// Policy: the battery's current charge/discharge behavior is treated as fixed household
// consumption/production and is never renegotiated to feed the EV (battery takes priority
// over EV charging). Under that assumption, "PV minus non-EV home consumption" algebraically
// reduces to (current EV power) - (current grid power):
//
//   HomeLoad + BatteryNet + EvPower = PV + Grid                    (energy balance)
//   Surplus  = PV - (HomeLoad + BatteryNet)
//            = PV - (PV + Grid - EvPower)
//            = EvPower - Grid
//
// This also happens to be the standard "target zero grid exchange" EVSE control law: if
// Grid is positive (importing), reduce below current EV power; if negative (exporting),
// increase above it. Battery power never appears explicitly because its current behavior
// is already folded into Grid.
public sealed class SolarSurplusChargingStrategy : IChargingStrategy
{
    private readonly double _nominalVoltage;
    private readonly double _minChargingCurrentAmps;
    private readonly double _maxChargingCurrentAmps;

    public SolarSurplusChargingStrategy(double nominalVoltage, double minChargingCurrentAmps, double maxChargingCurrentAmps)
    {
        _nominalVoltage = nominalVoltage;
        _minChargingCurrentAmps = minChargingCurrentAmps;
        _maxChargingCurrentAmps = maxChargingCurrentAmps;
    }

    public ChargingRecommendation Evaluate(EnergyState state)
    {
        var surplusWatts = state.EvChargerPowerWatts - state.GridPowerWatts;
        var uncappedAmps = surplusWatts / _nominalVoltage;
        var clampedAmps = Math.Clamp(uncappedAmps, 0, _maxChargingCurrentAmps);

        // Below the EVSE's minimum viable current (commonly 6A per IEC 61851), there's no
        // usable surplus -- a trickle current isn't something a charger can actually use.
        var isSurplusAvailable = clampedAmps >= _minChargingCurrentAmps;

        return new ChargingRecommendation(
            SurplusPowerWatts: surplusWatts,
            RecommendedChargingCurrentAmps: isSurplusAvailable ? clampedAmps : 0,
            IsSurplusAvailable: isSurplusAvailable);
    }
}
