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

    // The charger's raw register values from before we first overrode anything. Kept raw (rather than
    // the decoded whole-amp model) so a restore puts back exactly what the owner had -- no clamping to
    // 6-32A and no rounding of the 0.01A scale.
    private (ushort Mode, ushort Current)? _original;

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

    public bool HasOriginal => _original is not null;

    public async Task CaptureOriginalAsync(CancellationToken cancellationToken = default)
    {
        if (_original is not null)
        {
            return;
        }

        // Read the raw registers straight from the device (bypassing the dry-run simulation) so the
        // snapshot is the owner's true starting configuration.
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        var mode = await ReadRegisterAsync(EvChargerRegisterMap.ChargerUseMode, cancellationToken).ConfigureAwait(false);
        var current = await ReadRegisterAsync(EvChargerRegisterMap.ChargeCurrentSetpoint, cancellationToken).ConfigureAwait(false);

        _original = (mode, current);

        _logger.LogInformation(
            "Captured original charger settings for restore: Mode={Mode} (register {ModeRegister}), Current={Amps}A (register {CurrentRegister}).",
            (EvChargerMode)mode, mode, current * CurrentRegisterAmpsPerCount, current);
    }

    public async Task<bool> RestoreOriginalAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (_original is not (var originalMode, var originalCurrent))
        {
            return false;
        }

        var (activeMode, activeCurrent) = await ReadActiveRawAsync(cancellationToken).ConfigureAwait(false);
        var prefix = _dryRun ? "[DRY RUN] would restore " : "restoring ";

        // Written verbatim: these values came off the device, so they need no clamping or rounding.
        if (activeMode != originalMode)
        {
            _logger.LogInformation(
                "{Prefix}charger use-mode: {OldMode} -> {NewMode} (register {RegisterValue}). {Reason}",
                prefix, (EvChargerMode)activeMode, (EvChargerMode)originalMode, originalMode, reason);

            if (!_dryRun)
            {
                await _client
                    .WriteSingleRegisterAsync(EvChargerRegisterMap.ChargerUseMode.Address, originalMode, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (activeCurrent != originalCurrent)
        {
            _logger.LogInformation(
                "{Prefix}charger current setpoint: {OldAmps}A -> {NewAmps}A (register {RegisterValue}). {Reason}",
                prefix,
                activeCurrent * CurrentRegisterAmpsPerCount,
                originalCurrent * CurrentRegisterAmpsPerCount,
                originalCurrent,
                reason);

            if (!_dryRun)
            {
                await _client
                    .WriteSingleRegisterAsync(EvChargerRegisterMap.ChargeCurrentSetpoint.Address, originalCurrent, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Only released once every write above succeeded, so a failed restore is retried next cycle.
        _original = null;
        _simulated = null;
        return true;
    }

    // The raw registers currently active on the device -- or, in dry-run, the values our simulated
    // writes would have left there, so the restore log reflects the simulated timeline.
    private async Task<(ushort Mode, ushort Current)> ReadActiveRawAsync(CancellationToken cancellationToken)
    {
        if (_dryRun && _simulated is not null)
        {
            return ((ushort)_simulated.Mode, (ushort)Math.Round(_simulated.ChargeCurrentAmps / CurrentRegisterAmpsPerCount));
        }

        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        var mode = await ReadRegisterAsync(EvChargerRegisterMap.ChargerUseMode, cancellationToken).ConfigureAwait(false);
        var current = await ReadRegisterAsync(EvChargerRegisterMap.ChargeCurrentSetpoint, cancellationToken).ConfigureAwait(false);
        return (mode, current);
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
