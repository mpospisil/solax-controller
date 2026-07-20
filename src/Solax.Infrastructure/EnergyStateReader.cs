using Microsoft.Extensions.DependencyInjection;
using Solax.Core.Interfaces;
using Solax.Core.Models;
using Solax.Infrastructure.RegisterMaps;

namespace Solax.Infrastructure;

public sealed class EnergyStateReader : IEnergyStateReader
{
    private readonly IModbusClient _inverterClient;
    private readonly IModbusClient _evChargerClient;

    public EnergyStateReader(
        [FromKeyedServices(ModbusClientKeys.Inverter)] IModbusClient inverterClient,
        [FromKeyedServices(ModbusClientKeys.EvCharger)] IModbusClient evChargerClient)
    {
        _inverterClient = inverterClient;
        _evChargerClient = evChargerClient;
    }

    public async Task<EnergyState> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_inverterClient.IsConnected)
        {
            await _inverterClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!_evChargerClient.IsConnected)
        {
            await _evChargerClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        var batterySoc = await ReadAsync(_inverterClient, InverterRegisterMap.BatterySoc, cancellationToken).ConfigureAwait(false);
        var batteryPower = await ReadAsync(_inverterClient, InverterRegisterMap.BatteryPower, cancellationToken).ConfigureAwait(false);
        var pvPower = await ReadAsync(_inverterClient, InverterRegisterMap.PvPower, cancellationToken).ConfigureAwait(false);
        var gridPower = await ReadAsync(_inverterClient, InverterRegisterMap.GridPower, cancellationToken).ConfigureAwait(false);
        var evStatus = await ReadAsync(_evChargerClient, EvChargerRegisterMap.Status, cancellationToken).ConfigureAwait(false);
        var evPower = await ReadAsync(_evChargerClient, EvChargerRegisterMap.Power, cancellationToken).ConfigureAwait(false);

        return EnergyState.FromRawRegisters(DateTimeOffset.UtcNow, batterySoc, batteryPower, pvPower, gridPower, evStatus, evPower);
    }

    private static async Task<ushort> ReadAsync(
        IModbusClient client,
        RegisterDescriptor register,
        CancellationToken cancellationToken)
    {
        try
        {
            var values = await client
                .ReadHoldingRegistersAsync(register.Address, numberOfPoints: 1, cancellationToken)
                .ConfigureAwait(false);
            return values[0];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to read register '{register.Name}' at address {register.Address}.", ex);
        }
    }
}
