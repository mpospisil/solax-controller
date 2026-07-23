using Solax.Core.Enums;
using Solax.Core.Models;

namespace Solax.Core.Strategies;

/// <summary>
/// Drives the EV charger from <em>live</em> solar production instead of the forecast, and only once the
/// home battery is essentially full. The available surplus is the actual
/// <c>SolarPowerWatts - OtherLoadsPowerWatts</c> (equivalently EV + battery − grid: the power that
/// would otherwise be exported). The shared base handles the phase-aware setpoint, the 6 A minimum,
/// resume hysteresis, and disconnect handling.
///
/// A battery-SOC gate sits in front of the surplus logic: charging only engages at or above
/// <see cref="_fullSocPercent"/> (default 95%) and, once charging, keeps going until SOC falls below
/// <see cref="_releaseSocPercent"/> -- a hysteresis band so the car drawing solar (which can dip the
/// battery slightly) doesn't flap the gate on and off.
/// </summary>
public sealed class LiveSolarChargingController : SurplusChargingControllerBase
{
    private readonly double _fullSocPercent;
    private readonly double _releaseSocPercent;

    public LiveSolarChargingController(
        ChargePowerConverter powerConverter,
        int minChargingCurrentAmps,
        int maxChargingCurrentAmps,
        int currentStepAmps,
        double hysteresisWatts,
        double fullSocPercent,
        double releaseSocPercent)
        : base(powerConverter, minChargingCurrentAmps, maxChargingCurrentAmps, currentStepAmps, hysteresisWatts)
    {
        if (releaseSocPercent > fullSocPercent)
        {
            throw new ArgumentException(
                $"Release SOC ({releaseSocPercent}%) must not exceed the full SOC ({fullSocPercent}%).",
                nameof(releaseSocPercent));
        }

        _fullSocPercent = fullSocPercent;
        _releaseSocPercent = releaseSocPercent;
    }

    protected override string NoSignalReason => "No live solar surplus; leaving charger unchanged.";

    protected override ChargingControlDecision? Gate(ChargingControlInput input)
    {
        var soc = input.State.BatterySocPercent;

        // Hysteresis: engage only at/above the full threshold, but once engaged keep going down to
        // the release threshold, so EV load dipping the battery doesn't immediately close the gate.
        var currentlyCharging = IsCharging(input.CurrentSettings);
        var gateOpen = currentlyCharging ? soc >= _releaseSocPercent : soc >= _fullSocPercent;

        if (gateOpen)
        {
            return null;
        }

        var threshold = currentlyCharging ? _releaseSocPercent : _fullSocPercent;
        return input.HasControl
            ? Pause(input, $"Battery {soc:F0}% below {threshold:F0}% full-battery gate; pausing.")
            : new ChargingControlDecision(ChargingControlAction.None, null, $"Battery {soc:F0}% below {threshold:F0}%; waiting for a full battery before charging from solar.");
    }

    protected override double? AvailableSurplusWatts(ChargingControlInput input) =>
        input.State.SolarPowerWatts - input.State.OtherLoadsPowerWatts;
}
