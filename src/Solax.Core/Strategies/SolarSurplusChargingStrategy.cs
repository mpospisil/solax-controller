using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Core.Strategies;

// "Other Loads" here matches the residual load figure shown in the SolaX Cloud app: the
// portion of household consumption that isn't PV, EV, or battery, derived from the same
// energy balance the app itself uses (source: PV + Grid; sinks: OtherLoads + EV + Battery,
// using EnergyState's sign convention -- positive Grid/Battery = importing/charging):
//
//   OtherLoads = PV + Grid - EV - Battery
//   Surplus    = PV - OtherLoads
//              = PV - (PV + Grid - EV - Battery)
//              = EV + Battery - Grid
//
// Because OtherLoads nets out both EV and battery as individually-tracked consumers, this
// surplus is what's available to the EV charger *including* whatever the battery is
// currently drawing to charge -- i.e. the EV can outbid battery charging for surplus power.
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
        var surplusWatts = state.EvChargerPowerWatts + state.BatteryPowerWatts - state.GridPowerWatts;
        var uncappedAmps = surplusWatts / _nominalVoltage;
        var clampedAmps = Math.Clamp(uncappedAmps, 0, _maxChargingCurrentAmps);

        // Below the EVSE's minimum viable current (commonly 6A per IEC 61851), there's no
        // usable surplus -- a trickle current isn't something a charger can actually use.
        var isSurplusAvailable = clampedAmps >= _minChargingCurrentAmps;

        // Current solar production minus what's currently going into charging (EV + battery).
        // Only the charging component of battery power counts here -- discharging isn't
        // "charging" and doesn't add back into this figure.
        var batteryChargingWatts = Math.Max(state.BatteryPowerWatts, 0);
        var availableSolarPowerWatts = state.SolarPowerWatts - state.EvChargerPowerWatts - batteryChargingWatts;

        return new ChargingRecommendation(
            SurplusPowerWatts: surplusWatts,
            AvailableSolarPowerWatts: availableSolarPowerWatts,
            RecommendedChargingCurrentAmps: isSurplusAvailable ? clampedAmps : 0,
            IsSurplusAvailable: isSurplusAvailable);
    }
}
