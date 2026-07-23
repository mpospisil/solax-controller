using Solax.Core.Enums;

namespace Solax.Core.Models;

/// <summary>
/// The input a <see cref="Interfaces.IChargingController"/> needs to decide what to do with the
/// charger this cycle.
/// </summary>
/// <param name="State">The latest energy snapshot (carries charger status and the Other Loads residual).</param>
/// <param name="PredictedSolarPowerWatts">
/// Solcast's forecast PV power for this instant, in watts, or null when no forecast is available.
/// Used only by the forecast-driven controller; the live-solar controller reads actual PV from
/// <paramref name="State"/> and ignores this.
/// </param>
/// <param name="CurrentSettings">The charger's currently active settings, read from the hardware.</param>
/// <param name="HasControl">
/// Whether the orchestrator currently holds control (i.e. has backed up the original settings and
/// is actively driving the charger). Governs whether a disconnect should trigger a restore.
/// </param>
public sealed record ChargingControlInput(
    EnergyState State,
    double? PredictedSolarPowerWatts,
    EvChargerSettings CurrentSettings,
    bool HasControl);

/// <summary>
/// The controller's intent for this cycle. <see cref="TargetSettings"/> is populated for
/// <see cref="ChargingControlAction.Charge"/> and <see cref="ChargingControlAction.Pause"/>, and is
/// null for <see cref="ChargingControlAction.None"/> and <see cref="ChargingControlAction.Restore"/>.
/// <see cref="Reason"/> is a short human-readable explanation for logging.
/// </summary>
public sealed record ChargingControlDecision(
    ChargingControlAction Action,
    EvChargerSettings? TargetSettings,
    string Reason);
