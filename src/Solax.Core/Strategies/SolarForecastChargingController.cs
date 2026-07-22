using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Core.Strategies;

/// <summary>
/// Drives the EV charger from the solar <em>forecast</em>: while a car is connected, it fast-charges
/// on predicted surplus (predicted PV minus the home's Other Loads), pauses when the surplus falls
/// below the minimum viable charging power, and asks for the original settings to be restored once
/// the car unplugs.
///
/// The available power is <c>PredictedSolarPowerWatts - OtherLoadsPowerWatts</c>. It is converted to
/// a whole-amp setpoint the hardware accepts (floored to <see cref="_currentStepAmps"/> and clamped
/// to the min/max current). A hysteresis margin on the resume threshold prevents flapping around the
/// minimum as the forecast/loads jitter: charging starts only once the surplus clears
/// <c>minWatts + hysteresis</c>, but continues down to <c>minWatts</c>.
/// </summary>
public sealed class SolarForecastChargingController : IChargingController
{
    // Charger states in which a vehicle is connected and we may take control. Anything else that
    // isn't Available (Faulted, Unavailable, Update, ...) is left untouched to avoid fighting the
    // device.
    private static readonly HashSet<EvChargerStatus> ControllableStates =
    [
        EvChargerStatus.Preparing,
        EvChargerStatus.Charging,
        EvChargerStatus.SuspendedEv,
        EvChargerStatus.SuspendedEvse,
        EvChargerStatus.ChargePaused,
    ];

    private readonly double _nominalVoltage;
    private readonly int _minChargingCurrentAmps;
    private readonly int _maxChargingCurrentAmps;
    private readonly int _currentStepAmps;
    private readonly double _hysteresisWatts;

    public SolarForecastChargingController(
        double nominalVoltage,
        int minChargingCurrentAmps,
        int maxChargingCurrentAmps,
        int currentStepAmps,
        double hysteresisWatts)
    {
        if (nominalVoltage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalVoltage), nominalVoltage, "Nominal voltage must be positive.");
        }

        if (currentStepAmps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentStepAmps), currentStepAmps, "Current step must be positive.");
        }

        _nominalVoltage = nominalVoltage;
        _minChargingCurrentAmps = minChargingCurrentAmps;
        _maxChargingCurrentAmps = maxChargingCurrentAmps;
        _currentStepAmps = currentStepAmps;
        _hysteresisWatts = hysteresisWatts;
    }

    public ChargingControlDecision Decide(ChargingControlInput input)
    {
        var status = input.State.EvChargerStatus;

        if (status == EvChargerStatus.Available)
        {
            return input.HasControl
                ? new ChargingControlDecision(ChargingControlAction.Restore, null, "Car disconnected; restoring original charger settings.")
                : new ChargingControlDecision(ChargingControlAction.None, null, "No car connected.");
        }

        if (!ControllableStates.Contains(status))
        {
            return new ChargingControlDecision(ChargingControlAction.None, null, $"Charger state {status} is not controllable; leaving it untouched.");
        }

        var availableWatts = input.PredictedSolarPowerWatts - input.State.OtherLoadsPowerWatts;
        var minWatts = _minChargingCurrentAmps * _nominalVoltage;

        // Asymmetric threshold: keep charging down to the minimum, but only (re)start once we're a
        // hysteresis margin above it, so a forecast hovering near the minimum doesn't flap.
        var currentlyCharging = IsCharging(input.CurrentSettings);
        var startThresholdWatts = currentlyCharging ? minWatts : minWatts + _hysteresisWatts;

        if (availableWatts < startThresholdWatts)
        {
            return Pause(input, $"Predicted surplus {availableWatts:F0}W below {(currentlyCharging ? "minimum" : "resume")} threshold {startThresholdWatts:F0}W; pausing.");
        }

        var targetAmps = ToHardwareCurrent(availableWatts);
        if (targetAmps < _minChargingCurrentAmps)
        {
            return Pause(input, $"Predicted surplus {availableWatts:F0}W quantises below minimum {_minChargingCurrentAmps}A; pausing.");
        }

        return new ChargingControlDecision(
            ChargingControlAction.Charge,
            new EvChargerSettings(EvChargerMode.Fast, targetAmps),
            $"Predicted surplus {availableWatts:F0}W -> fast charge at {targetAmps}A.");
    }

    // Pausing switches the use-mode to Stop but keeps the existing current setpoint: the charge
    // current register has a 6A hardware minimum, so writing 0 would be an invalid value. Leaving the
    // current unchanged means only the mode register is actually written.
    private static ChargingControlDecision Pause(ChargingControlInput input, string reason) =>
        new(ChargingControlAction.Pause,
            new EvChargerSettings(EvChargerMode.Stop, input.CurrentSettings.ChargeCurrentAmps),
            reason);

    private bool IsCharging(EvChargerSettings settings) =>
        settings.Mode == EvChargerMode.Fast && settings.ChargeCurrentAmps >= _minChargingCurrentAmps;

    // Converts available watts to a whole-amp setpoint the charger accepts: divide by voltage, floor
    // to the current step, then clamp to the charger's max. May return below the minimum, which the
    // caller treats as "pause".
    private int ToHardwareCurrent(double availableWatts)
    {
        var rawAmps = availableWatts / _nominalVoltage;
        var steppedAmps = (int)Math.Floor(rawAmps / _currentStepAmps) * _currentStepAmps;
        return Math.Min(steppedAmps, _maxChargingCurrentAmps);
    }
}
