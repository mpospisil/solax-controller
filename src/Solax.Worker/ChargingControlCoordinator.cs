using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Worker;

/// <summary>
/// Orchestrates one charge-control cycle: reads the charger's current settings, asks the
/// <see cref="IChargingController"/> what to do, and applies it — backing up the owner's original
/// settings before the first override and restoring them when the car disconnects.
///
/// Holds the backup state across cycles, so it is registered as a singleton. All hardware errors
/// are caught and logged so a control failure never disrupts the polling loop. Only invoked when
/// <c>ChargeControl:Enabled</c> is true.
/// </summary>
public sealed class ChargingControlCoordinator
{
    private readonly IChargingController _controller;
    private readonly IEvChargerControl _chargerControl;
    private readonly ILogger<ChargingControlCoordinator> _logger;

    public ChargingControlCoordinator(
        IChargingController controller,
        IEvChargerControl chargerControl,
        ILogger<ChargingControlCoordinator> logger)
    {
        _controller = controller;
        _chargerControl = chargerControl;
        _logger = logger;
    }

    public async Task RunCycleAsync(EnergyState state, CancellationToken cancellationToken)
    {
        try
        {
            var current = await _chargerControl.ReadSettingsAsync(cancellationToken).ConfigureAwait(false);

            var input = new ChargingControlInput(
                state,
                CurrentSettings: current,
                HasControl: _chargerControl.HasOriginal);

            var decision = _controller.Decide(input);

            switch (decision.Action)
            {
                case ChargingControlAction.None:
                    break;

                case ChargingControlAction.Restore:
                    await RestoreAsync(cancellationToken).ConfigureAwait(false);
                    break;

                case ChargingControlAction.Charge:
                case ChargingControlAction.Pause:
                    await ApplyTakingControlAsync(current, decision, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Charge-control cycle failed; will retry next poll.");
        }
    }

    /// <summary>
    /// Puts the charger back to the owner's original settings when the service is shutting down, so we
    /// never leave our override behind. Safe to call when we don't hold control (it's a no-op), and
    /// failures are logged rather than blocking shutdown.
    /// </summary>
    public async Task RestoreOnShutdownAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (await _chargerControl
                    .RestoreOriginalAsync("Service stopping; restoring original charger settings.", cancellationToken)
                    .ConfigureAwait(false))
            {
                _logger.LogInformation("Released charge control on shutdown; original charger settings restored.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore original charger settings on shutdown; the charger may still hold our override.");
        }
    }

    private async Task ApplyTakingControlAsync(
        EvChargerSettings current,
        ChargingControlDecision decision,
        CancellationToken cancellationToken)
    {
        // Snapshot the owner's original settings before the first override (no-op afterwards), so
        // every value we change can be put back exactly on disconnect.
        await _chargerControl.CaptureOriginalAsync(cancellationToken).ConfigureAwait(false);

        // ApplyAsync writes only the fields that differ and logs each change.
        await _chargerControl
            .ApplyAsync(current, decision.TargetSettings!, decision.Reason, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        var restored = await _chargerControl
            .RestoreOriginalAsync("Car disconnected; restoring original charger settings.", cancellationToken)
            .ConfigureAwait(false);

        if (restored)
        {
            _logger.LogInformation("Released charge control; original charger settings restored.");
        }
    }
}
