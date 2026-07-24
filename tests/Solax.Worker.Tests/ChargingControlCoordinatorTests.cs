using Microsoft.Extensions.Logging.Abstractions;
using Solax.Core.Enums;
using Solax.Core.Models;

namespace Solax.Worker.Tests;

public class ChargingControlCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 11, 0, 0, TimeSpan.Zero);
    private static readonly EvChargerSettings Original = new(EvChargerMode.Eco, 8);

    private readonly FakeEvChargerControl _charger = new();
    private readonly StubChargingController _controller = new();
    private readonly ChargingControlCoordinator _coordinator;

    public ChargingControlCoordinatorTests()
    {
        _coordinator = new ChargingControlCoordinator(_controller, _charger, NullLogger<ChargingControlCoordinator>.Instance);
    }

    private static EnergyState State(EvChargerStatus status) =>
        new(Now, BatterySocPercent: 50, BatteryPowerWatts: 0, SolarPowerWatts: 0, GridPowerWatts: 0, status, EvChargerPowerWatts: 0);

    private Task Cycle(EvChargerStatus status) => _coordinator.RunCycleAsync(State(status), CancellationToken.None);

    [Fact]
    public async Task ChargeDecision_BacksUpOriginalAndAppliesTarget()
    {
        _charger.CurrentSettings = Original;
        _controller.NextDecision = new(ChargingControlAction.Charge, new EvChargerSettings(EvChargerMode.Fast, 10), "charge");

        await Cycle(EvChargerStatus.Charging);

        var applied = Assert.Single(_charger.Applied);
        Assert.Equal(Original, applied.Current);
        Assert.Equal(new EvChargerSettings(EvChargerMode.Fast, 10), applied.Target);
        Assert.False(_controller.LastInput!.HasControl); // no control held before this first cycle
    }

    [Fact]
    public async Task Disconnect_RestoresBackedUpOriginalAndReleasesControl()
    {
        _charger.CurrentSettings = Original;
        _controller.NextDecision = new(ChargingControlAction.Charge, new EvChargerSettings(EvChargerMode.Fast, 10), "charge");
        await Cycle(EvChargerStatus.Charging); // takes control, backs up Original

        _controller.NextDecision = new(ChargingControlAction.Restore, null, "disconnect");
        await Cycle(EvChargerStatus.Available); // restore

        Assert.Equal(Original, _charger.Restored); // every original value put back
        Assert.False(_charger.HasOriginal); // control released

        // A further restore does nothing.
        await Cycle(EvChargerStatus.Available);
        Assert.Single(_charger.Applied); // only the original charge write, no extra writes
    }

    [Fact]
    public async Task Backup_IsCapturedOnce_AndSurvivesSetpointChanges()
    {
        _charger.CurrentSettings = Original;
        _controller.NextDecision = new(ChargingControlAction.Charge, new EvChargerSettings(EvChargerMode.Fast, 10), "charge");
        await Cycle(EvChargerStatus.Charging); // backs up Original, applies Fast/10 (now current)

        _controller.NextDecision = new(ChargingControlAction.Charge, new EvChargerSettings(EvChargerMode.Fast, 16), "more sun");
        await Cycle(EvChargerStatus.Charging); // must NOT re-backup Fast/10
        Assert.True(_controller.LastInput!.HasControl);

        _controller.NextDecision = new(ChargingControlAction.Restore, null, "disconnect");
        await Cycle(EvChargerStatus.Available);

        Assert.Equal(Original, _charger.Restored); // still the very first original, not Fast/10
    }

    [Fact]
    public async Task NoneDecision_WritesNothing()
    {
        _controller.NextDecision = new(ChargingControlAction.None, null, "idle");

        await Cycle(EvChargerStatus.Charging);

        Assert.Empty(_charger.Applied);
    }

    [Fact]
    public async Task RestoreDecision_WithoutControl_WritesNothing()
    {
        _controller.NextDecision = new(ChargingControlAction.Restore, null, "spurious");

        await Cycle(EvChargerStatus.Available);

        Assert.Empty(_charger.Applied);
    }
}
