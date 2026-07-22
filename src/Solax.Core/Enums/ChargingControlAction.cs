namespace Solax.Core.Enums;

/// <summary>
/// What the <see cref="Interfaces.IChargingController"/> wants done to the charger this cycle.
/// The controller only decides intent; applying it (reading current settings, writing only on
/// change, backup/restore, logging) is the orchestrator's job.
/// </summary>
public enum ChargingControlAction
{
    /// <summary>Do nothing — the controller is not taking control this cycle.</summary>
    None,

    /// <summary>Charge: apply the decision's target settings (fast mode at the target current).</summary>
    Charge,

    /// <summary>Pause charging while keeping control (the car is still connected).</summary>
    Pause,

    /// <summary>The car is disconnected — restore the backed-up original settings and release control.</summary>
    Restore,
}
