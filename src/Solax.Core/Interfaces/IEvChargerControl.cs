using Solax.Core.Models;

namespace Solax.Core.Interfaces;

/// <summary>
/// Reads and writes the EV charger's controllable settings. Writes are idempotent: only registers
/// whose target differs from the currently active value are written, and every actual change is
/// logged. This is where the "write only on change" and "log every hardware change" rules live.
/// </summary>
public interface IEvChargerControl
{
    /// <summary>Reads the charger's currently active settings (use-mode and current setpoint).</summary>
    Task<EvChargerSettings> ReadSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="target"/> to the charger, but only the fields that differ from
    /// <paramref name="current"/> (the freshly-read active settings). Each change is logged with its
    /// old → new value and <paramref name="reason"/>. Returns the settings now active on the device.
    /// </summary>
    Task<EvChargerSettings> ApplyAsync(
        EvChargerSettings current,
        EvChargerSettings target,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Whether an original-settings snapshot is currently held (i.e. we hold control).</summary>
    bool HasOriginal { get; }

    /// <summary>
    /// Snapshots the charger's original settings before we first override them, so they can be put
    /// back exactly. No-op if a snapshot is already held. Captures the raw register values, not the
    /// decoded model, so the restore is byte-exact.
    /// </summary>
    Task CaptureOriginalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the captured original settings back to the charger verbatim — every value we changed
    /// (use-mode and current setpoint), without the safety clamping/rounding applied to computed
    /// setpoints, since these values came from the device itself. Only registers that actually differ
    /// are written, each change is logged, and the snapshot is released on success. Returns false when
    /// there was no snapshot to restore.
    /// </summary>
    Task<bool> RestoreOriginalAsync(string reason, CancellationToken cancellationToken = default);
}
