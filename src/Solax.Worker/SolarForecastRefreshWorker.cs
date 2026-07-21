using Microsoft.Extensions.Options;
using Solax.Infrastructure.Solcast;

namespace Solax.Worker;

/// <summary>
/// Drives <see cref="SolcastForecastService.RefreshAsync"/>: fetches the forecast once at startup
/// (so the cache is warm) and then re-fetches on the configured <see cref="SolcastOptions.RefreshInterval"/>.
/// Hosting this as a background service is also what causes the singleton forecast service to be
/// instantiated when the application starts.
/// </summary>
public sealed class SolarForecastRefreshWorker : BackgroundService
{
    private readonly SolcastForecastService _forecastService;
    private readonly ILogger<SolarForecastRefreshWorker> _logger;
    private readonly TimeSpan _refreshInterval;

    public SolarForecastRefreshWorker(
        SolcastForecastService forecastService,
        IOptions<SolcastOptions> options,
        ILogger<SolarForecastRefreshWorker> logger)
    {
        _forecastService = forecastService;
        _logger = logger;
        _refreshInterval = options.Value.RefreshInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Solcast forecast refresh started; interval {RefreshInterval}.", _refreshInterval);

        // RefreshAsync swallows its own failures (keeping any cached forecast), so the loop here
        // just paces the calls -- a bad fetch doesn't need to stop future refreshes.
        while (!stoppingToken.IsCancellationRequested)
        {
            await _forecastService.RefreshAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(_refreshInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
