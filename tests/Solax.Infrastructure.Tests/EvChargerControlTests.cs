using Microsoft.Extensions.Logging.Abstractions;
using Solax.Core.Enums;
using Solax.Core.Models;
using Solax.Infrastructure.RegisterMaps;

namespace Solax.Infrastructure.Tests;

public class EvChargerControlTests
{
    private static readonly ushort ModeAddress = EvChargerRegisterMap.ChargerUseMode.Address;
    private static readonly ushort CurrentAddress = EvChargerRegisterMap.ChargeCurrentSetpoint.Address;

    private static EvChargerControl Create(FakeModbusClient client) =>
        new(client, NullLogger<EvChargerControl>.Instance);

    private static EvChargerControl CreateDryRun(FakeModbusClient client) =>
        new(client, NullLogger<EvChargerControl>.Instance, dryRun: true);

    [Fact]
    public async Task ReadSettingsAsync_DecodesCurrentFrom001AScale()
    {
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Eco);
        client.SetHolding(CurrentAddress, 800); // 0.01A scale -> 8A

        var settings = await Create(client).ReadSettingsAsync();

        Assert.Equal(new EvChargerSettings(EvChargerMode.Eco, 8), settings);
    }

    [Fact]
    public async Task ApplyAsync_NoChange_WritesNothing()
    {
        var client = new FakeModbusClient();
        var settings = new EvChargerSettings(EvChargerMode.Fast, 10);

        await Create(client).ApplyAsync(current: settings, target: settings, "no change");

        Assert.Empty(client.Writes);
    }

    [Fact]
    public async Task ApplyAsync_OnlyModeDiffers_WritesOnlyModeRegister()
    {
        var client = new FakeModbusClient();

        await Create(client).ApplyAsync(
            current: new EvChargerSettings(EvChargerMode.Stop, 10),
            target: new EvChargerSettings(EvChargerMode.Fast, 10),
            "mode only");

        Assert.Equal([(ModeAddress, (ushort)EvChargerMode.Fast)], client.Writes);
    }

    [Fact]
    public async Task ApplyAsync_OnlyCurrentDiffers_WritesCurrentEncodedWith001AScale()
    {
        var client = new FakeModbusClient();

        await Create(client).ApplyAsync(
            current: new EvChargerSettings(EvChargerMode.Fast, 6),
            target: new EvChargerSettings(EvChargerMode.Fast, 16),
            "current only");

        Assert.Equal([(CurrentAddress, (ushort)1600)], client.Writes); // 16A * 100
    }

    [Fact]
    public async Task ApplyAsync_BothDiffer_WritesBothRegisters()
    {
        var client = new FakeModbusClient();

        await Create(client).ApplyAsync(
            current: new EvChargerSettings(EvChargerMode.Stop, 6),
            target: new EvChargerSettings(EvChargerMode.Fast, 12),
            "both");

        Assert.Contains((ModeAddress, (ushort)EvChargerMode.Fast), client.Writes);
        Assert.Contains((CurrentAddress, (ushort)1200), client.Writes); // 12A * 100
        Assert.Equal(2, client.Writes.Count);
    }

    [Fact]
    public async Task ApplyAsync_CurrentAboveHardwareMax_ClampsTo32A()
    {
        var client = new FakeModbusClient();

        await Create(client).ApplyAsync(
            current: new EvChargerSettings(EvChargerMode.Fast, 10),
            target: new EvChargerSettings(EvChargerMode.Fast, 40), // beyond the 32A hardware max
            "clamp");

        Assert.Equal([(CurrentAddress, (ushort)3200)], client.Writes); // clamped to 32A -> 3200
    }

    [Fact]
    public async Task RestoreOriginalAsync_PutsBackBothModeAndCurrent()
    {
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Eco);
        client.SetHolding(CurrentAddress, 1600); // 16A
        var control = Create(client);

        await control.CaptureOriginalAsync();
        Assert.True(control.HasOriginal);

        // We take over: Fast at 20A.
        await control.ApplyAsync(new EvChargerSettings(EvChargerMode.Eco, 16), new EvChargerSettings(EvChargerMode.Fast, 20), "charge");
        client.Writes.Clear();

        Assert.True(await control.RestoreOriginalAsync("disconnect"));

        // Every changed value is put back: mode AND current setpoint.
        Assert.Contains((ModeAddress, (ushort)EvChargerMode.Eco), client.Writes);
        Assert.Contains((CurrentAddress, (ushort)1600), client.Writes);
        Assert.Equal(2, client.Writes.Count);
        Assert.False(control.HasOriginal); // snapshot released
    }

    [Fact]
    public async Task RestoreOriginalAsync_RestoresValuesOutsideTheSafetyClampVerbatim()
    {
        // The 6-32A clamp guards computed setpoints; a restore must put back exactly what the device
        // had, even if that's outside the clamp (here 0), instead of silently writing 6A.
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Stop);
        client.SetHolding(CurrentAddress, 0);
        var control = Create(client);

        await control.CaptureOriginalAsync();
        await control.ApplyAsync(new EvChargerSettings(EvChargerMode.Stop, 0), new EvChargerSettings(EvChargerMode.Fast, 20), "charge");
        client.Writes.Clear();

        await control.RestoreOriginalAsync("disconnect");

        Assert.Contains((CurrentAddress, (ushort)0), client.Writes); // verbatim 0, not clamped to 600
    }

    [Fact]
    public async Task RestoreOriginalAsync_PreservesSubAmpPrecision()
    {
        // 1650 = 16.5A. The whole-amp model would round this to 17A (1700); the raw snapshot restores
        // the exact original register value.
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Green);
        client.SetHolding(CurrentAddress, 1650);
        var control = Create(client);

        await control.CaptureOriginalAsync();
        await control.ApplyAsync(new EvChargerSettings(EvChargerMode.Green, 17), new EvChargerSettings(EvChargerMode.Fast, 20), "charge");
        client.Writes.Clear();

        await control.RestoreOriginalAsync("disconnect");

        Assert.Contains((CurrentAddress, (ushort)1650), client.Writes);
    }

    [Fact]
    public async Task RestoreOriginalAsync_WritesNothingWhenAlreadyAtOriginal()
    {
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Eco);
        client.SetHolding(CurrentAddress, 1600);
        var control = Create(client);

        await control.CaptureOriginalAsync();
        client.Writes.Clear();

        Assert.True(await control.RestoreOriginalAsync("disconnect"));

        Assert.Empty(client.Writes); // nothing was changed, so nothing to put back
    }

    [Fact]
    public async Task CaptureOriginalAsync_IsIdempotent_KeepingTheFirstSnapshot()
    {
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Eco);
        client.SetHolding(CurrentAddress, 1600);
        var control = Create(client);

        await control.CaptureOriginalAsync();
        await control.ApplyAsync(new EvChargerSettings(EvChargerMode.Eco, 16), new EvChargerSettings(EvChargerMode.Fast, 20), "charge");
        await control.CaptureOriginalAsync(); // must NOT re-snapshot the overridden state
        client.Writes.Clear();

        await control.RestoreOriginalAsync("disconnect");

        Assert.Contains((ModeAddress, (ushort)EvChargerMode.Eco), client.Writes);
        Assert.Contains((CurrentAddress, (ushort)1600), client.Writes);
    }

    [Fact]
    public async Task RestoreOriginalAsync_WithoutSnapshot_ReturnsFalseAndWritesNothing()
    {
        var client = new FakeModbusClient();

        Assert.False(await Create(client).RestoreOriginalAsync("spurious"));

        Assert.Empty(client.Writes);
    }

    [Fact]
    public async Task DryRun_RestoreOriginalAsync_WritesNothingToHardware()
    {
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Eco);
        client.SetHolding(CurrentAddress, 1600);
        var control = CreateDryRun(client);

        await control.CaptureOriginalAsync();
        await control.ApplyAsync(new EvChargerSettings(EvChargerMode.Eco, 16), new EvChargerSettings(EvChargerMode.Fast, 20), "charge");
        client.Writes.Clear();

        Assert.True(await control.RestoreOriginalAsync("disconnect"));

        Assert.Empty(client.Writes);
    }

    [Fact]
    public async Task DryRun_ApplyAsync_WritesNothingToHardware()
    {
        var client = new FakeModbusClient();

        await CreateDryRun(client).ApplyAsync(
            current: new EvChargerSettings(EvChargerMode.Stop, 6),
            target: new EvChargerSettings(EvChargerMode.Fast, 16),
            "dry run");

        Assert.Empty(client.Writes);
    }

    [Fact]
    public async Task DryRun_ReadSettings_ReflectsPriorSimulatedApply()
    {
        var client = new FakeModbusClient();
        client.SetHolding(ModeAddress, (ushort)EvChargerMode.Green);
        client.SetHolding(CurrentAddress, 600); // 6A
        var control = CreateDryRun(client);

        var target = new EvChargerSettings(EvChargerMode.Fast, 16);
        await control.ApplyAsync(await control.ReadSettingsAsync(), target, "dry run");

        // No hardware write, but the next read reflects the simulated state.
        Assert.Empty(client.Writes);
        Assert.Equal(target, await control.ReadSettingsAsync());
    }
}
