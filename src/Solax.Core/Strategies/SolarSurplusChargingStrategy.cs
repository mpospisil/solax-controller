using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Core.Strategies;

// ChargingMode.SolarAndBattery: "Other Loads" here matches the residual load figure shown in
// the SolaX Cloud app: the portion of household consumption that isn't PV, EV, or battery,
// derived from the same energy balance the app itself uses (source: PV + Grid; sinks:
// OtherLoads + EV + Battery, using EnergyState's sign convention -- positive Grid/Battery =
// importing/charging):
//
//   OtherLoads = PV + Grid - EV - Battery
//   Target     = PV - OtherLoads
//              = PV - (PV + Grid - EV - Battery)
//              = EV + Battery - Grid
//
// Because OtherLoads nets out both EV and battery as individually-tracked consumers, this
// target is what's available to the EV charger *including* whatever the battery is currently
// drawing to charge -- i.e. the EV can outbid battery charging for surplus power.
//
// ChargingMode.SolarOnly: the battery is never touched, so the target is simply current EV
// power plus AvailableSolarPowerWatts (solar minus EV minus battery-charging), which reduces to
// solar production minus whatever the battery is currently drawing to charge:
//
//   Target = EV + AvailableSolarPowerWatts
//          = EV + (PV - EV - BatteryCharging)
//          = PV - BatteryCharging
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

    public ChargingRecommendation Evaluate(EnergyState state, ChargingMode mode)
    {
        var targetWatts = mode switch
        {
            // EV + AvailableSolarPowerWatts (solar minus EV minus battery-charging) reduces to
            // solar production minus whatever the battery is currently drawing to charge.
            ChargingMode.SolarOnly => state.EvChargerPowerWatts + state.AvailableSolarPowerWatts,
            ChargingMode.SolarAndBattery => state.EvChargerPowerWatts + state.BatteryPowerWatts - state.GridPowerWatts,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported charging mode."),
        };

        var uncappedAmps = targetWatts / _nominalVoltage;
        var clampedAmps = Math.Clamp(uncappedAmps, 0, _maxChargingCurrentAmps);

        // Below the EVSE's minimum viable current (commonly 6A per IEC 61851), there's no
        // usable surplus -- a trickle current isn't something a charger can actually use.
        var isSurplusAvailable = clampedAmps >= _minChargingCurrentAmps;
        var recommendedAmps = isSurplusAvailable ? clampedAmps : 0;

        return new ChargingRecommendation(
            SurplusPowerWatts: targetWatts,
            RecommendedChargingPowerWatts: recommendedAmps * _nominalVoltage,
            RecommendedChargingCurrentAmps: recommendedAmps,
            IsSurplusAvailable: isSurplusAvailable);
    }
}
