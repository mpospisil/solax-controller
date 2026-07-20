using Microsoft.Extensions.Options;
using Serilog;
using Solax.Core.Interfaces;
using Solax.Infrastructure;
using Solax.Infrastructure.Modbus;
using Solax.Worker;
using Solax.Worker.Configuration;

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
builder.Services.AddHostedService<SolaxPollingService>();

var host = builder.Build();
host.Run();
