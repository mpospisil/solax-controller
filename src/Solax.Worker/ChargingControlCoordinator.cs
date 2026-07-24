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

    // True once we've overridden the charger, until it has been reset back to the idle state.
    private bool _hasControl;

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
                HasControl: _hasControl);

            var decision = _controller.Decide(input);

            switch (decision.Action)
            {
                case ChargingControlAction.None:
                    break;

                case ChargingControlAction.Charge:
                    await ChargeAsync(current, decision, cancellationToken).ConfigureAwait(false);
                    break;

                case ChargingControlAction.Pause:
                    await ResetToIdleAsync(decision.Reason, cancellationToken).ConfigureAwait(false);
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
    /// Resets the charger to the idle state when the service is shutting down, so we never leave our
    /// override behind. No-op when we don't hold control, and failures are logged rather than blocking
    /// shutdown.
    /// </summary>
    public async Task ResetOnShutdownAsync(CancellationToken cancellationToken)
    {
        if (!_hasControl)
        {
            return;
        }

        try
        {
            await ResetToIdleAsync("Service stopping.", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset the charger on shutdown; it may still hold our override.");
        }
    }

    private async Task ChargeAsync(
        EvChargerSettings current,
        ChargingControlDecision decision,
        CancellationToken cancellationToken)
    {
        var starting = !_hasControl;

        // ApplyAsync writes only the fields that differ and logs each change.
        await _chargerControl
            .ApplyAsync(current, decision.TargetSettings!, decision.Reason, cancellationToken)
            .ConfigureAwait(false);

        // The charger may be sitting idle/stopped (e.g. after our own reset), so a mode change alone
        // won't necessarily begin a session -- issue the start command, but only on the transition so
        // we don't re-send it every poll while charging.
        if (starting)
        {
            await _chargerControl
                .SendCommandAsync(EvChargerControlCommand.StartCharging, decision.Reason, cancellationToken)
                .ConfigureAwait(false);
            _hasControl = true;
            _logger.LogInformation("Took charge control; started charging from live solar surplus.");
        }
    }

    private async Task ResetToIdleAsync(string reason, CancellationToken cancellationToken)
    {
        if (!_hasControl)
        {
            return;
        }

        await _chargerControl.ResetAsync(reason, cancellationToken).ConfigureAwait(false);

        // Only released once the reset writes succeeded, so a failed reset is retried next cycle.
        _hasControl = false;
        _logger.LogInformation(
            "Released charge control; charger reset to Stop at {Amps}A with a stop-charging command.",
            EvChargerLimits.MinCurrentAmps);
    }
}
