using Microsoft.Extensions.Options;
using Solax.Core.Interfaces;
using Solax.Worker.Configuration;

namespace Solax.Worker;

public sealed class SolaxPollingService : BackgroundService
{
    private readonly IEnergyStateReader _energyStateReader;
    private readonly ILogger<SolaxPollingService> _logger;
    private readonly TimeSpan _pollInterval;

    public SolaxPollingService(
        IEnergyStateReader energyStateReader,
        IOptions<SolaxOptions> options,
        ILogger<SolaxPollingService> logger)
    {
        _energyStateReader = energyStateReader;
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
                    "SOC={BatterySocPercent}% BatteryPower={BatteryPowerWatts}W PV={PvPowerWatts}W Grid={GridPowerWatts}W EvCharger={EvChargerStatus} EvPower={EvChargerPowerWatts}W",
                    state.BatterySocPercent,
                    state.BatteryPowerWatts,
                    state.PvPowerWatts,
                    state.GridPowerWatts,
                    state.EvChargerStatus,
                    state.EvChargerPowerWatts);
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
}
