using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;

namespace Solax.Worker;

/// <summary>
/// Orchestrates one forecast-driven charge-control cycle: reads the charger's current settings,
/// asks the <see cref="IChargingController"/> what to do, and applies it — backing up the owner's
/// original settings before the first override and restoring them when the car disconnects.
///
/// Holds the backup state across cycles, so it is registered as a singleton. All hardware errors
/// are caught and logged so a control failure never disrupts the polling loop. Only invoked when
/// <c>ChargeControl:Enabled</c> is true.
/// </summary>
public sealed class ChargingControlCoordinator
{
    private readonly IChargingController _controller;
    private readonly IEvChargerControl _chargerControl;
    private readonly ISolarForecastService _forecast;
    private readonly ILogger<ChargingControlCoordinator> _logger;

    // Non-null while we hold control: the settings the charger had before we first overrode them,
    // to be written back on disconnect.
    private EvChargerSettings? _originalSettings;

    public ChargingControlCoordinator(
        IChargingController controller,
        IEvChargerControl chargerControl,
        ISolarForecastService forecast,
        ILogger<ChargingControlCoordinator> logger)
    {
        _controller = controller;
        _chargerControl = chargerControl;
        _forecast = forecast;
        _logger = logger;
    }

    public async Task RunCycleAsync(EnergyState state, CancellationToken cancellationToken)
    {
        try
        {
            var current = await _chargerControl.ReadSettingsAsync(cancellationToken).ConfigureAwait(false);

            // Fetched for the forecast strategy; passed through as nullable so the controller decides
            // what a missing forecast means (the live-solar strategy ignores it entirely).
            var forecastNow = _forecast.GetForecastForToday()?.ExpectedPowerWattsAt(state.Timestamp);

            var input = new ChargingControlInput(
                state,
                PredictedSolarPowerWatts: forecastNow,
                CurrentSettings: current,
                HasControl: _originalSettings is not null);

            var decision = _controller.Decide(input);

            switch (decision.Action)
            {
                case ChargingControlAction.None:
                    break;

                case ChargingControlAction.Restore:
                    await RestoreAsync(current, cancellationToken).ConfigureAwait(false);
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

    private async Task ApplyTakingControlAsync(
        EvChargerSettings current,
        ChargingControlDecision decision,
        CancellationToken cancellationToken)
    {
        if (_originalSettings is null)
        {
            _originalSettings = current;
            _logger.LogInformation(
                "Taking charge control; backed up original charger settings (Mode={Mode}, Current={Amps}A).",
                current.Mode, current.ChargeCurrentAmps);
        }

        // ApplyAsync writes only the fields that differ and logs each change.
        await _chargerControl
            .ApplyAsync(current, decision.TargetSettings!, decision.Reason, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RestoreAsync(EvChargerSettings current, CancellationToken cancellationToken)
    {
        if (_originalSettings is null)
        {
            return;
        }

        await _chargerControl
            .ApplyAsync(current, _originalSettings, "Car disconnected; restoring original charger settings.", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Released charge control; original charger settings restored.");
        _originalSettings = null;
    }
}
