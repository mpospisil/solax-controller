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
/// In dry-run mode nothing is written: each intended change is logged (including the encoded
/// register value) and an internal simulated state stands in for the hardware, so the logs read
/// like a real run (change once, then quiet) without touching the charger.
///
/// !! The control register addresses come from the SolaX X1/X3-HAC protocol but still depend on
/// your GEN/firmware -- verify before enabling real writes. Control is disabled by default.
/// </summary>
public sealed class EvChargerControl : IEvChargerControl
{
    // The SolaX charge-current holding register stores hundredths of an amp: registerValue = amps * 100
    // (16A -> 1600). Values are constrained to the hardware's 6-32A range on write, no matter what the
    // caller asks for, so we can never send a current the charger rejects.
    private const double CurrentRegisterAmpsPerCount = 0.01;
    private const int HardwareMinCurrentAmps = 6;
    private const int HardwareMaxCurrentAmps = 32;

    private readonly IModbusClient _client;
    private readonly ILogger<EvChargerControl> _logger;
    private readonly bool _dryRun;

    // Dry-run only: the settings the charger "would" now have, so reads reflect prior simulated
    // writes and change-detection behaves like a real run instead of re-logging every poll.
    private EvChargerSettings? _simulated;

    // The known safe idle state written when we release control (see ResetAsync).
    private static readonly EvChargerSettings ResetSettings = new(EvChargerMode.Stop, HardwareMinCurrentAmps);

    public EvChargerControl(
        [FromKeyedServices(ModbusClientKeys.EvCharger)] IModbusClient client,
        ILogger<EvChargerControl> logger,
        bool dryRun = false)
    {
        _client = client;
        _logger = logger;
        _dryRun = dryRun;
    }

    public async Task<EvChargerSettings> ReadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_dryRun && _simulated is not null)
        {
            return _simulated;
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        var mode = await ReadRegisterAsync(EvChargerRegisterMap.ChargerUseMode, cancellationToken).ConfigureAwait(false);
        var currentRaw = await ReadRegisterAsync(EvChargerRegisterMap.ChargeCurrentSetpoint, cancellationToken).ConfigureAwait(false);

        // Decode the 0.01A register scale back to whole amps.
        var currentAmps = (int)Math.Round(currentRaw * CurrentRegisterAmpsPerCount);
        return new EvChargerSettings((EvChargerMode)mode, currentAmps);
    }

    public async Task<EvChargerSettings> ApplyAsync(
        EvChargerSettings current,
        EvChargerSettings target,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!_dryRun)
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        }

        var prefix = _dryRun ? "[DRY RUN] would set " : "";

        if (current.Mode != target.Mode)
        {
            _logger.LogInformation(
                "{Prefix}charger use-mode: {OldMode} -> {NewMode}. {Reason}",
                prefix, current.Mode, target.Mode, reason);

            if (!_dryRun)
            {
                await _client
                    .WriteSingleRegisterAsync(EvChargerRegisterMap.ChargerUseMode.Address, (ushort)target.Mode, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (current.ChargeCurrentAmps != target.ChargeCurrentAmps)
        {
            // Clamp to the hardware's accepted range, then encode with the 0.01A register scale.
            var clampedAmps = Math.Clamp(target.ChargeCurrentAmps, HardwareMinCurrentAmps, HardwareMaxCurrentAmps);
            var registerValue = (ushort)Math.Round(clampedAmps / CurrentRegisterAmpsPerCount);

            _logger.LogInformation(
                "{Prefix}charger current setpoint: {OldAmps}A -> {NewAmps}A (register {RegisterValue}). {Reason}",
                prefix, current.ChargeCurrentAmps, clampedAmps, registerValue, reason);

            if (!_dryRun)
            {
                await _client
                    .WriteSingleRegisterAsync(EvChargerRegisterMap.ChargeCurrentSetpoint.Address, registerValue, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (_dryRun)
        {
            _simulated = target;
        }

        return target;
    }

    public async Task ResetAsync(string reason, CancellationToken cancellationToken = default)
    {
        // Mode and current are persistent settings, so reuse the normal change-detecting write path.
        var current = await ReadSettingsAsync(cancellationToken).ConfigureAwait(false);
        await ApplyAsync(current, ResetSettings, reason, cancellationToken).ConfigureAwait(false);

        // The control command is an action rather than a stored setting, so it is always issued
        // (there is nothing meaningful to compare it against).
        var prefix = _dryRun ? "[DRY RUN] would send " : "sending ";
        _logger.LogInformation(
            "{Prefix}charger control command: {Command} (register {RegisterValue}). {Reason}",
            prefix, EvChargerControlCommand.StopCharging, (ushort)EvChargerControlCommand.StopCharging, reason);

        if (!_dryRun)
        {
            await _client
                .WriteSingleRegisterAsync(
                    EvChargerRegisterMap.ControlCommand.Address,
                    (ushort)EvChargerControlCommand.StopCharging,
                    cancellationToken)
                .ConfigureAwait(false);
        }
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
