using Microsoft.Extensions.Options;
using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Worker.Configuration;

namespace Solax.Worker;

public sealed class SolaxPollingService : BackgroundService
{
    private readonly IEnergyStateReader _energyStateReader;
    private readonly IChargingStrategy _chargingStrategy;
    private readonly ISolarForecastService _solarForecast;
    private readonly ILogger<SolaxPollingService> _logger;
    private readonly TimeSpan _pollInterval;

    public SolaxPollingService(
        IEnergyStateReader energyStateReader,
        IChargingStrategy chargingStrategy,
        ISolarForecastService solarForecast,
        IOptions<SolaxOptions> options,
        ILogger<SolaxPollingService> logger)
    {
        _energyStateReader = energyStateReader;
        _chargingStrategy = chargingStrategy;
        _solarForecast = solarForecast;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var state = await _energyStateReader.ReadAsync(stoppingToken);

                _logger.LogInformation(
                    "SOC={BatterySocPercent}% BatteryPower={BatteryPowerWatts}W Solar={SolarPowerWatts}W Grid={GridPowerWatts}W EvCharger={EvChargerStatus} EvPower={EvChargerPowerWatts}W",
                    state.BatterySocPercent,
                    state.BatteryPowerWatts,
                    state.SolarPowerWatts,
                    state.GridPowerWatts,
                    state.EvChargerStatus,
                    state.EvChargerPowerWatts);

                LogSolarForecast(state.Timestamp);

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

    // Logs the solar generation Solcast expects right now (plus today's shape), from the locally
    // cached forecast. Null until the first successful fetch completes -- log-only for now; the
    // forecast doesn't yet feed the charging strategy.
    private void LogSolarForecast(DateTimeOffset now)
    {
        var today = _solarForecast.GetForecastForToday();
        if (today is null)
        {
            return;
        }

        var expectedNow = today.ExpectedPowerWattsAt(now);

        _logger.LogInformation(
            "Solar forecast: ExpectedNow={ExpectedSolarPowerWatts}W PeakToday={PeakPowerWatts}W ExpectedEnergyToday={ExpectedEnergyWattHours}Wh",
            expectedNow is null ? "n/a" : $"{expectedNow.Value:F0}",
            $"{today.PeakPowerWatts:F0}",
            $"{today.ExpectedEnergyWattHours:F0}");
    }
}
