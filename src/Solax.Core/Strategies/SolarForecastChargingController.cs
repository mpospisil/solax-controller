using Solax.Core.Models;

namespace Solax.Core.Strategies;

/// <summary>
/// Drives the EV charger from the solar <em>forecast</em>: while a car is connected it fast-charges on
/// predicted surplus (predicted PV minus the home's Other Loads), pauses when the surplus falls below
/// the minimum viable charging power, and asks for the original settings to be restored on disconnect.
/// The available power is <c>PredictedSolarPowerWatts - OtherLoadsPowerWatts</c>; the shared base
/// handles the phase-aware setpoint, hysteresis, and disconnect handling.
/// </summary>
public sealed class SolarForecastChargingController : SurplusChargingControllerBase
{
    public SolarForecastChargingController(
        ChargePowerConverter powerConverter,
        int minChargingCurrentAmps,
        int maxChargingCurrentAmps,
        int currentStepAmps,
        double hysteresisWatts)
        : base(powerConverter, minChargingCurrentAmps, maxChargingCurrentAmps, currentStepAmps, hysteresisWatts)
    {
    }

    protected override string NoSignalReason => "No solar forecast available yet; leaving charger unchanged.";

    protected override double? AvailableSurplusWatts(ChargingControlInput input) =>
        input.PredictedSolarPowerWatts is null
            ? null
            : input.PredictedSolarPowerWatts.Value - input.State.OtherLoadsPowerWatts;
}
