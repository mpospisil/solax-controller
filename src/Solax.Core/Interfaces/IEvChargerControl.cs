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

    /// <summary>
    /// Returns the charger to a known safe idle state when we release control: use-mode
    /// <see cref="Enums.EvChargerMode.Stop"/>, the minimum 6 A current setpoint, and a
    /// <see cref="Enums.EvChargerControlCommand.StopCharging"/> command. The mode and current are
    /// written only if they differ from what's active; the stop command is always issued (it is a
    /// command, not a persistent setting). Every write is logged.
    /// </summary>
    Task ResetAsync(string reason, CancellationToken cancellationToken = default);
}
