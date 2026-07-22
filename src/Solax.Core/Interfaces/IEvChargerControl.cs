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
}
