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

        var inverterBlock = await ReadInverterTelemetryBlockAsync(cancellationToken).ConfigureAwait(false);

        var evStatus = await ReadAsync(_evChargerClient, EvChargerRegisterMap.RunMode, cancellationToken).ConfigureAwait(false);
        var evPower = await ReadAsync(_evChargerClient, EvChargerRegisterMap.ChargePowerTotal, cancellationToken).ConfigureAwait(false);

        return EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: FromBlock(inverterBlock, InverterRegisterMap.BatteryCapacity),
            batteryPowerRaw: FromBlock(inverterBlock, InverterRegisterMap.BatteryPowerCharge1),
            pvPowerDc1Raw: FromBlock(inverterBlock, InverterRegisterMap.Powerdc1),
            pvPowerDc2Raw: FromBlock(inverterBlock, InverterRegisterMap.Powerdc2),
            gridPowerRRaw: FromBlock(inverterBlock, InverterRegisterMap.GridPowerR),
            gridPowerSRaw: FromBlock(inverterBlock, InverterRegisterMap.GridPowerS),
            gridPowerTRaw: FromBlock(inverterBlock, InverterRegisterMap.GridPowerT),
            evChargerStatusRaw: evStatus,
            evChargerPowerRaw: evPower);
    }

    // Reads the whole telemetry range in one Modbus request (the SolaX protocol requires
    // >=1 second between separate instructions, so batching beats many small reads).
    private async Task<ushort[]> ReadInverterTelemetryBlockAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _inverterClient
                .ReadInputRegistersAsync(
                    InverterRegisterMap.TelemetryBlockStart,
                    InverterRegisterMap.TelemetryBlockCount,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Failed to read inverter telemetry block starting at address {InverterRegisterMap.TelemetryBlockStart}.", ex);
        }
    }

    private static ushort FromBlock(ushort[] block, RegisterDescriptor register) => block[register.Address];

    private static async Task<ushort> ReadAsync(
        IModbusClient client,
        RegisterDescriptor register,
        CancellationToken cancellationToken)
    {
        try
        {
            var values = await client
                .ReadInputRegistersAsync(register.Address, numberOfPoints: 1, cancellationToken)
                .ConfigureAwait(false);
            return values[0];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to read register '{register.Name}' at address {register.Address}.", ex);
        }
    }
}
