using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Core.Strategies;

/// <summary>
/// Drives the EV charger from <em>live</em> solar surplus, and only once the home battery is
/// essentially full. The available surplus is the actual <c>SolarPowerWatts - OtherLoadsPowerWatts</c>
/// (equivalently EV + battery − grid: the power that would otherwise be exported). It is converted to
/// a hardware-legal, phase-aware whole-amp setpoint, clamped to the charger's min/max, with resume
/// hysteresis so a surplus hovering near the minimum doesn't flap the charger.
///
/// A battery-SOC gate sits in front of the surplus logic: charging only engages at or above
/// <see cref="_fullSocPercent"/> (default 95%) and, once charging, keeps going until SOC falls below
/// <see cref="_releaseSocPercent"/> — a hysteresis band so the car drawing solar (which can dip the
/// battery slightly) doesn't flap the gate on and off. On disconnect it asks for the original charger
/// settings to be restored.
/// </summary>
public sealed class LiveSolarChargingController : IChargingController
{
    // Charger states in which a vehicle is connected and we may take control. Anything else that
    // isn't Available (Faulted, Unavailable, Update, ...) is left untouched to avoid fighting it.
    private static readonly HashSet<EvChargerStatus> ControllableStates =
    [
        EvChargerStatus.Preparing,
        EvChargerStatus.Charging,
        EvChargerStatus.SuspendedEv,
        EvChargerStatus.SuspendedEvse,
        EvChargerStatus.ChargePaused,
    ];

    private readonly ChargePowerConverter _power;
    private readonly int _minChargingCurrentAmps;
    private readonly int _maxChargingCurrentAmps;
    private readonly int _currentStepAmps;
    private readonly double _hysteresisWatts;
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
    {
        if (currentStepAmps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentStepAmps), currentStepAmps, "Current step must be positive.");
        }

        if (releaseSocPercent > fullSocPercent)
        {
            throw new ArgumentException(
                $"Release SOC ({releaseSocPercent}%) must not exceed the full SOC ({fullSocPercent}%).",
                nameof(releaseSocPercent));
        }

        _power = powerConverter ?? throw new ArgumentNullException(nameof(powerConverter));
        _minChargingCurrentAmps = minChargingCurrentAmps;
        _maxChargingCurrentAmps = maxChargingCurrentAmps;
        _currentStepAmps = currentStepAmps;
        _hysteresisWatts = hysteresisWatts;
        _fullSocPercent = fullSocPercent;
        _releaseSocPercent = releaseSocPercent;
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

        var currentlyCharging = IsCharging(input.CurrentSettings);

        // Battery-SOC gate with hysteresis: engage only at/above the full threshold, but once engaged
        // keep going down to the release threshold, so EV load dipping the battery doesn't immediately
        // close the gate.
        var soc = input.State.BatterySocPercent;
        var gateOpen = currentlyCharging ? soc >= _releaseSocPercent : soc >= _fullSocPercent;
        if (!gateOpen)
        {
            var threshold = currentlyCharging ? _releaseSocPercent : _fullSocPercent;
            return input.HasControl
                ? Pause(input, $"Battery {soc:F0}% below {threshold:F0}% full-battery gate; pausing.")
                : new ChargingControlDecision(ChargingControlAction.None, null, $"Battery {soc:F0}% below {threshold:F0}%; waiting for a full battery before charging from solar.");
        }

        var availableWatts = input.State.SolarPowerWatts - input.State.OtherLoadsPowerWatts;
        var minWatts = _power.AmpsToWatts(_minChargingCurrentAmps);

        // Asymmetric threshold: keep charging down to the minimum, but only (re)start once we're a
        // hysteresis margin above it.
        var startThresholdWatts = currentlyCharging ? minWatts : minWatts + _hysteresisWatts;
        if (availableWatts < startThresholdWatts)
        {
            return Pause(input, $"Live surplus {availableWatts:F0}W below {(currentlyCharging ? "minimum" : "resume")} threshold {startThresholdWatts:F0}W; pausing.");
        }

        var targetAmps = ToHardwareCurrent(availableWatts);
        if (targetAmps < _minChargingCurrentAmps)
        {
            return Pause(input, $"Live surplus {availableWatts:F0}W quantises below minimum {_minChargingCurrentAmps}A; pausing.");
        }

        return new ChargingControlDecision(
            ChargingControlAction.Charge,
            new EvChargerSettings(EvChargerMode.Fast, targetAmps),
            $"Live surplus {availableWatts:F0}W -> fast charge at {targetAmps}A.");
    }

    private bool IsCharging(EvChargerSettings settings) =>
        settings.Mode == EvChargerMode.Fast && settings.ChargeCurrentAmps >= _minChargingCurrentAmps;

    // Converts available watts to a whole-amp setpoint the charger accepts: convert to amps
    // (phase-aware), floor to the current step, then clamp to the charger's max. May return below the
    // minimum, which the caller treats as "pause".
    private int ToHardwareCurrent(double availableWatts)
    {
        var rawAmps = _power.WattsToAmps(availableWatts);
        var steppedAmps = (int)Math.Floor(rawAmps / _currentStepAmps) * _currentStepAmps;
        return Math.Min(steppedAmps, _maxChargingCurrentAmps);
    }

    // Pausing switches the use-mode to Stop but keeps the existing current setpoint: the charge
    // current register has a 6A hardware minimum, so writing 0 would be an invalid value.
    private static ChargingControlDecision Pause(ChargingControlInput input, string reason) =>
        new(ChargingControlAction.Pause,
            new EvChargerSettings(EvChargerMode.Stop, input.CurrentSettings.ChargeCurrentAmps),
            reason);
}
