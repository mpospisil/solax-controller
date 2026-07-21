namespace Solax.Core.Models;

/// <summary>
/// The output of an <see cref="Interfaces.IChargingStrategy"/> evaluation: how much power/current
/// is currently available for EV charging, derived from a single <see cref="EnergyState"/>
/// snapshot under a given <see cref="Enums.ChargingMode"/>.
/// </summary>
/// <param name="SurplusPowerWatts">
/// Total power, in watts, that could go to the EV charger right now, before clamping to the
/// charger's min/max current range. The formula depends on the requested
/// <see cref="Enums.ChargingMode"/> -- see <see cref="Strategies.SolarSurplusChargingStrategy"/>
/// for the derivation of each mode. Positive means there's room to increase charging; negative
/// means the EV is currently oversubscribed relative to what's available. This is the
/// (unclamped) basis for <see cref="RecommendedChargingPowerWatts"/> and
/// <see cref="RecommendedChargingCurrentAmps"/>.
/// </param>
/// <param name="RecommendedChargingPowerWatts">
/// <see cref="SurplusPowerWatts"/> clamped to the charger's configured min/max current range and
/// converted back to watts. 0 when <see cref="IsSurplusAvailable"/> is false.
/// </param>
/// <param name="RecommendedChargingCurrentAmps">
/// <see cref="RecommendedChargingPowerWatts"/> expressed as amps (i.e. divided by the configured
/// nominal voltage). 0 when <see cref="IsSurplusAvailable"/> is false.
/// </param>
/// <param name="IsSurplusAvailable">
/// Whether the computed surplus, once converted to a current, is at or above the charger's
/// configured minimum viable current (commonly 6A per IEC 61851). Below that threshold there's
/// no usable surplus -- a trickle current isn't something a charger can actually use.
/// </param>
public sealed record ChargingRecommendation(
    double SurplusPowerWatts,
    double RecommendedChargingPowerWatts,
    double RecommendedChargingCurrentAmps,
    bool IsSurplusAvailable);
