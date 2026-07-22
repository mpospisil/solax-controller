using Microsoft.Extensions.Options;
using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;
using Solax.Worker.Configuration;

namespace Solax.Worker;

public sealed class SolaxPollingService : BackgroundService
{
    private readonly IEnergyStateReader _energyStateReader;
    private readonly IChargingStrategy _chargingStrategy;
    private readonly ISolarForecastService _solarForecast;
    private readonly ChargingControlCoordinator _chargingControl;
    private readonly bool _chargeControlEnabled;
    private readonly bool _chargeControlDryRun;
    private readonly ILogger<SolaxPollingService> _logger;
    private readonly TimeSpan _pollInterval;

    public SolaxPollingService(
        IEnergyStateReader energyStateReader,
        IChargingStrategy chargingStrategy,
        ISolarForecastService solarForecast,
        ChargingControlCoordinator chargingControl,
        IOptions<SolaxOptions> options,
        IOptions<ChargeControlOptions> chargeControlOptions,
        ILogger<SolaxPollingService> logger)
    {
        _energyStateReader = energyStateReader;
        _chargingStrategy = chargingStrategy;
        _solarForecast = solarForecast;
        _chargingControl = chargingControl;
        _chargeControlEnabled = chargeControlOptions.Value.Enabled;
        _chargeControlDryRun = chargeControlOptions.Value.DryRun;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_chargeControlEnabled)
        {
            _logger.LogInformation(
                "Forecast-driven charge control is ENABLED ({Mode}).",
                _chargeControlDryRun ? "DRY RUN — no writes to the charger" : "live — writing to the charger");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var state = await _energyStateReader.ReadAsync(stoppingToken);

                _logger.LogInformation(
                    "SOC={BatterySocPercent}% BatteryPower={BatteryPowerWatts}W Solar={SolarPowerWatts}W Grid={GridPowerWatts}W EvCharger={EvChargerStatus} EvMode={EvChargeMode} EvPower={EvChargerPowerWatts}W",
                    state.BatterySocPercent,
                    state.BatteryPowerWatts,
                    state.SolarPowerWatts,
                    state.GridPowerWatts,
                    state.EvChargerStatus,
                    (object?)state.ChargeMode ?? "n/a",
                    state.EvChargerPowerWatts);

                LogSolarActualVsForecast(state);

                if (_chargeControlEnabled)
                {
                    await _chargingControl.RunCycleAsync(state, stoppingToken);
                }

                if (state.EvChargerStatus == EvChargerStatus.Charging)
                {
                    var recommendation = _chargingStrategy.Evaluate(state, ChargingMode.SolarOnly);

                    _logger.LogInformation(
                        "EV charging surplus: Surplus={SurplusPowerWatts}W AvailableSolar={AvailableSolarPowerWatts}W RecommendedPower={RecommendedChargingPowerWatts}W RecommendedCurrent={RecommendedChargingCurrentAmps}A Available={IsSurplusAvailable}",
                        recommendation.SurplusPowerWatts,
                        state.AvailableSolarPowerWatts,
                        recommendation.RecommendedChargingPowerWatts,
                        recommendation.RecommendedChargingCurrentAmps,
                        recommendation.IsSurplusAvailable);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A single failed poll (e.g. dropped connection, Modbus timeout) must not
                // take the service down — log and retry on the next tick.
                _logger.LogWarning(ex, "Failed to poll SolaX devices; will retry on next interval.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Logs actual solar generation against what Solcast forecast for this moment, plus their
    // delta (actual minus forecast: positive = producing more than predicted). The forecast comes
    // from the locally cached forecast and is null until the first successful fetch completes;
    // the day's overall shape is logged once per refresh inside the forecast service, not here.
    private void LogSolarActualVsForecast(EnergyState state)
    {
        var forecastNow = _solarForecast.GetForecastForToday()?.ExpectedPowerWattsAt(state.Timestamp);

        if (forecastNow is null)
        {
            _logger.LogInformation(
                "Solar: Actual={SolarPowerWatts:F0}W Forecast=n/a Delta=n/a",
                state.SolarPowerWatts);
            return;
        }

        _logger.LogInformation(
            "Solar: Actual={SolarPowerWatts:F0}W Forecast={ForecastPowerWatts:F0}W Delta={SolarDeltaWatts:F0}W",
            state.SolarPowerWatts,
            forecastNow.Value,
            state.SolarPowerWatts - forecastNow.Value);
    }
}
