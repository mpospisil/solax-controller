using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Serilog;
using Solax.Core.Interfaces;
using Solax.Core.Strategies;
using Solax.Infrastructure;
using Solax.Infrastructure.Modbus;
using Solax.Infrastructure.Solcast;
using Solax.Worker;
using Solax.Worker.Configuration;

// Load secrets (e.g. Solcast__ApiKey) from an untracked .env file into the process environment
// before configuration is built, so they reach the app whether it's started via `dotnet run` or
// the VS Code debugger -- without living in any committed file. Real env vars still take priority.
DotEnv.Load(Directory.GetCurrentDirectory());

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Services.AddSerilog(config => config
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext());

builder.Services.Configure<SolaxOptions>(builder.Configuration.GetSection(SolaxOptions.SectionName));

builder.Services.AddKeyedSingleton<IModbusClient>(ModbusClientKeys.Inverter, (services, _) =>
{
    var options = services.GetRequiredService<IOptions<SolaxOptions>>().Value;
    return new ModbusTcpClient(options.Inverter);
});

builder.Services.AddKeyedSingleton<IModbusClient>(ModbusClientKeys.EvCharger, (services, _) =>
{
    var options = services.GetRequiredService<IOptions<SolaxOptions>>().Value;
    return new ModbusTcpClient(options.EvCharger);
});

builder.Services.AddSingleton<IEnergyStateReader, EnergyStateReader>();

builder.Services.AddSingleton<IChargingStrategy>(services =>
{
    var options = services.GetRequiredService<IOptions<SolaxOptions>>().Value.ChargingStrategy;
    return new SolarSurplusChargingStrategy(
        options.NominalVoltage,
        options.MinChargingCurrentAmps,
        options.MaxChargingCurrentAmps);
});

// Solcast solar-forecast integration. The API key is a secret and is not stored in
// appsettings.json -- supply it via user-secrets (development) or an environment variable
// (deployment): Solcast:ApiKey / Solcast__ApiKey.
builder.Services.Configure<SolcastOptions>(builder.Configuration.GetSection(SolcastOptions.SectionName));

builder.Services.AddHttpClient(SolcastForecastService.HttpClientName, (services, client) =>
{
    var options = services.GetRequiredService<IOptions<SolcastOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    }

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }
});

// Single instance shared as both the injectable query interface and (via the refresh worker) a
// service warmed at startup.
builder.Services.AddSingleton<SolcastForecastService>();
builder.Services.AddSingleton<ISolarForecastService>(services => services.GetRequiredService<SolcastForecastService>());
builder.Services.AddHostedService<SolarForecastRefreshWorker>();

builder.Services.AddHostedService<SolaxPollingService>();

var host = builder.Build();
host.Run();
