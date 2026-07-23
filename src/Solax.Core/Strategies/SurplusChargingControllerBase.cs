using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Core.Strategies;

/// <summary>
/// Shared logic for surplus-based charge controllers: while a car is connected, charge on the
/// available surplus (converted to a hardware-legal, phase-aware whole-amp setpoint), pause below the
/// minimum viable current with resume hysteresis, and ask for the original settings to be restored on
/// disconnect. Subclasses supply where the "available surplus" comes from (forecast vs. live solar)
/// and any extra gate (e.g. a battery-SOC gate).
/// </summary>
public abstract class SurplusChargingControllerBase : IChargingController
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

    private readonly int _minChargingCurrentAmps;
    private readonly int _maxChargingCurrentAmps;
    private readonly int _currentStepAmps;
    private readonly double _hysteresisWatts;

    protected SurplusChargingControllerBase(
        ChargePowerConverter powerConverter,
        int minChargingCurrentAmps,
        int maxChargingCurrentAmps,
        int currentStepAmps,
        double hysteresisWatts)
    {
        if (currentStepAmps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentStepAmps), currentStepAmps, "Current step must be positive.");
        }

        Power = powerConverter ?? throw new ArgumentNullException(nameof(powerConverter));
        _minChargingCurrentAmps = minChargingCurrentAmps;
        _maxChargingCurrentAmps = maxChargingCurrentAmps;
        _currentStepAmps = currentStepAmps;
        _hysteresisWatts = hysteresisWatts;
    }

    protected ChargePowerConverter Power { get; }

    protected int MinChargingCurrentAmps => _minChargingCurrentAmps;

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

        // Subclass gate (e.g. battery SOC). A non-null result short-circuits the surplus logic.
        var gated = Gate(input);
        if (gated is not null)
        {
            return gated;
        }

        var availableWatts = AvailableSurplusWatts(input);
        if (availableWatts is null)
        {
            return new ChargingControlDecision(ChargingControlAction.None, null, NoSignalReason);
        }

        return ChargeOrPause(input, availableWatts.Value);
    }

    /// <summary>The surplus power available to the car this cycle, or null when there's no usable signal.</summary>
    protected abstract double? AvailableSurplusWatts(ChargingControlInput input);

    /// <summary>Reason logged when <see cref="AvailableSurplusWatts"/> returns null.</summary>
    protected virtual string NoSignalReason => "No control signal available; leaving charger unchanged.";

    /// <summary>Optional extra gate applied before the surplus logic; return a decision to short-circuit.</summary>
    protected virtual ChargingControlDecision? Gate(ChargingControlInput input) => null;

    protected bool IsCharging(EvChargerSettings settings) =>
        settings.Mode == EvChargerMode.Fast && settings.ChargeCurrentAmps >= _minChargingCurrentAmps;

    // Pausing switches the use-mode to Stop but keeps the existing current setpoint: the charge
    // current register has a 6A hardware minimum, so writing 0 would be an invalid value.
    protected static ChargingControlDecision Pause(ChargingControlInput input, string reason) =>
        new(ChargingControlAction.Pause,
            new EvChargerSettings(EvChargerMode.Stop, input.CurrentSettings.ChargeCurrentAmps),
            reason);

    private ChargingControlDecision ChargeOrPause(ChargingControlInput input, double availableWatts)
    {
        var minWatts = Power.AmpsToWatts(_minChargingCurrentAmps);

        // Asymmetric threshold: keep charging down to the minimum, but only (re)start once we're a
        // hysteresis margin above it, so a surplus hovering near the minimum doesn't flap.
        var currentlyCharging = IsCharging(input.CurrentSettings);
        var startThresholdWatts = currentlyCharging ? minWatts : minWatts + _hysteresisWatts;

        if (availableWatts < startThresholdWatts)
        {
            return Pause(input, $"Surplus {availableWatts:F0}W below {(currentlyCharging ? "minimum" : "resume")} threshold {startThresholdWatts:F0}W; pausing.");
        }

        var targetAmps = ToHardwareCurrent(availableWatts);
        if (targetAmps < _minChargingCurrentAmps)
        {
            return Pause(input, $"Surplus {availableWatts:F0}W quantises below minimum {_minChargingCurrentAmps}A; pausing.");
        }

        return new ChargingControlDecision(
            ChargingControlAction.Charge,
            new EvChargerSettings(EvChargerMode.Fast, targetAmps),
            $"Surplus {availableWatts:F0}W -> fast charge at {targetAmps}A.");
    }

    // Converts available watts to a whole-amp setpoint the charger accepts: convert to amps
    // (phase-aware), floor to the current step, then clamp to the charger's max. May return below the
    // minimum, which the caller treats as "pause".
    private int ToHardwareCurrent(double availableWatts)
    {
        var rawAmps = Power.WattsToAmps(availableWatts);
        var steppedAmps = (int)Math.Floor(rawAmps / _currentStepAmps) * _currentStepAmps;
        return Math.Min(steppedAmps, _maxChargingCurrentAmps);
    }
}
