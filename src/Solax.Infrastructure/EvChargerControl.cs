using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Solax.Core.Enums;
using Solax.Core.Interfaces;
using Solax.Core.Models;
using Solax.Infrastructure.RegisterMaps;

namespace Solax.Infrastructure;

/// <summary>
/// <see cref="IEvChargerControl"/> over Modbus. Reads the charger's use-mode and current-setpoint
/// holding registers, and writes only the ones that actually change, logging each change.
///
/// !! The control register addresses (see <see cref="EvChargerRegisterMap"/> /
/// <see cref="EvChargerRegister"/>) are UNVERIFIED placeholders. Verify them against your hardware
/// before enabling control. Control is disabled by default.
/// </summary>
public sealed class EvChargerControl : IEvChargerControl
{
    private readonly IModbusClient _client;
    private readonly ILogger<EvChargerControl> _logger;

    public EvChargerControl(
        [FromKeyedServices(ModbusClientKeys.EvCharger)] IModbusClient client,
        ILogger<EvChargerControl> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<EvChargerSettings> ReadSettingsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var mode = await ReadRegisterAsync(EvChargerRegisterMap.ChargerUseMode, cancellationToken).ConfigureAwait(false);
        var current = await ReadRegisterAsync(EvChargerRegisterMap.ChargeCurrentSetpoint, cancellationToken).ConfigureAwait(false);

        return new EvChargerSettings((EvChargerMode)mode, current);
    }

    public async Task<EvChargerSettings> ApplyAsync(
        EvChargerSettings current,
        EvChargerSettings target,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        if (current.Mode != target.Mode)
        {
            _logger.LogInformation(
                "Charger use-mode change: {OldMode} -> {NewMode}. {Reason}",
                current.Mode, target.Mode, reason);
            await _client
                .WriteSingleRegisterAsync(EvChargerRegisterMap.ChargerUseMode.Address, (ushort)target.Mode, cancellationToken)
                .ConfigureAwait(false);
        }

        if (current.ChargeCurrentAmps != target.ChargeCurrentAmps)
        {
            _logger.LogInformation(
                "Charger current setpoint change: {OldAmps}A -> {NewAmps}A. {Reason}",
                current.ChargeCurrentAmps, target.ChargeCurrentAmps, reason);
            await _client
                .WriteSingleRegisterAsync(EvChargerRegisterMap.ChargeCurrentSetpoint.Address, (ushort)target.ChargeCurrentAmps, cancellationToken)
                .ConfigureAwait(false);
        }

        return target;
    }

    private async Task<ushort> ReadRegisterAsync(RegisterDescriptor register, CancellationToken cancellationToken)
    {
        try
        {
            var values = await _client
                .ReadHoldingRegistersAsync(register.Address, numberOfPoints: 1, cancellationToken)
                .ConfigureAwait(false);
            return values[0];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to read charger register '{register.Name}' at address {register.Address}.", ex);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsConnected)
        {
            await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
