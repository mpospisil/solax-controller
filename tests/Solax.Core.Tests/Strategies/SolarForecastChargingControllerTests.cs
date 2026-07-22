using Solax.Core.Enums;
using Solax.Core.Models;
using Solax.Core.Strategies;

namespace Solax.Core.Tests.Strategies;

public class SolarForecastChargingControllerTests
{
    // 230V, 6-20A, 1A steps, 200W hysteresis on resume. minWatts = 6 * 230 = 1380W.
    private static readonly SolarForecastChargingController Controller = new(
        nominalVoltage: 230,
        minChargingCurrentAmps: 6,
        maxChargingCurrentAmps: 20,
        currentStepAmps: 1,
        hysteresisWatts: 200);

    private static readonly EvChargerSettings Charging10A = new(EvChargerMode.Fast, 10);
    private static readonly EvChargerSettings Stopped = new(EvChargerMode.Stop, 0);

    // Builds a state whose OtherLoads resolves to `otherLoads`. OtherLoads = PV + Grid - EV - Battery;
    // with PV/EV/Battery all 0, Grid alone drives it, so Grid = otherLoads.
    private static EnergyState StateWith(EvChargerStatus status, double otherLoads) =>
        new(
            DateTimeOffset.UtcNow,
            BatterySocPercent: 50,
            BatteryPowerWatts: 0,
            SolarPowerWatts: 0,
            GridPowerWatts: otherLoads,
            EvChargerStatus: status,
            EvChargerPowerWatts: 0);

    private static ChargingControlInput Input(
        EvChargerStatus status,
        double predictedSolarWatts,
        double otherLoadsWatts,
        EvChargerSettings currentSettings,
        bool hasControl) =>
        new(StateWith(status, otherLoadsWatts), predictedSolarWatts, currentSettings, hasControl);

    [Fact]
    public void Available_WithControl_RestoresOriginal()
    {
        var result = Controller.Decide(Input(EvChargerStatus.Available, 5000, 0, Stopped, hasControl: true));

        Assert.Equal(ChargingControlAction.Restore, result.Action);
        Assert.Null(result.TargetSettings);
    }

    [Fact]
    public void Available_WithoutControl_DoesNothing()
    {
        var result = Controller.Decide(Input(EvChargerStatus.Available, 5000, 0, Stopped, hasControl: false));

        Assert.Equal(ChargingControlAction.None, result.Action);
    }

    [Fact]
    public void FaultedOrOtherState_IsLeftUntouched()
    {
        var result = Controller.Decide(Input(EvChargerStatus.Faulted, 5000, 0, Stopped, hasControl: true));

        Assert.Equal(ChargingControlAction.None, result.Action);
    }

    [Fact]
    public void Connected_SurplusAboveResumeThreshold_StartsFastChargeAtQuantisedCurrent()
    {
        // Predicted 4600W, OtherLoads 500W -> 4100W surplus -> 4100/230 = 17.8A -> floored to 17A.
        var result = Controller.Decide(Input(EvChargerStatus.Preparing, 4600, 500, Stopped, hasControl: true));

        Assert.Equal(ChargingControlAction.Charge, result.Action);
        Assert.Equal(new EvChargerSettings(EvChargerMode.Fast, 17), result.TargetSettings);
    }

    [Fact]
    public void Connected_SurplusExceedsMax_ClampsToMaxCurrent()
    {
        // Huge surplus -> would be far above 20A, must clamp to 20A.
        var result = Controller.Decide(Input(EvChargerStatus.Charging, 20000, 0, Charging10A, hasControl: true));

        Assert.Equal(ChargingControlAction.Charge, result.Action);
        Assert.Equal(20, result.TargetSettings!.ChargeCurrentAmps);
    }

    [Fact]
    public void Connected_NotCharging_SurplusBetweenMinAndResumeThreshold_StaysPaused()
    {
        // Surplus 1450W: above minWatts (1380) but below resume threshold (1380+200=1580).
        // Since we're not currently charging, hysteresis keeps us paused.
        var result = Controller.Decide(Input(EvChargerStatus.ChargePaused, 1450, 0, Stopped, hasControl: true));

        Assert.Equal(ChargingControlAction.Pause, result.Action);
    }

    [Fact]
    public void Connected_AlreadyCharging_SurplusBetweenMinAndResumeThreshold_KeepsCharging()
    {
        // Same 1450W surplus, but already charging: we keep charging down to minWatts (no flap).
        // 1450/230 = 6.3A -> floored to 6A.
        var result = Controller.Decide(Input(EvChargerStatus.Charging, 1450, 0, Charging10A, hasControl: true));

        Assert.Equal(ChargingControlAction.Charge, result.Action);
        Assert.Equal(6, result.TargetSettings!.ChargeCurrentAmps);
    }

    [Fact]
    public void Connected_AlreadyCharging_SurplusBelowMinimum_Pauses()
    {
        // Surplus 1000W < minWatts 1380 -> pause even though currently charging.
        var result = Controller.Decide(Input(EvChargerStatus.Charging, 1000, 0, Charging10A, hasControl: true));

        Assert.Equal(ChargingControlAction.Pause, result.Action);
        // Pause switches to Stop but keeps the existing current (writing 0 would be below the 6A min).
        Assert.Equal(new EvChargerSettings(EvChargerMode.Stop, 10), result.TargetSettings);
    }

    [Fact]
    public void Connected_OtherLoadsConsumeSurplus_Pauses()
    {
        // Plenty of predicted solar but the home is eating it all: 3000 predicted - 2800 loads = 200W.
        var result = Controller.Decide(Input(EvChargerStatus.Charging, 3000, 2800, Charging10A, hasControl: true));

        Assert.Equal(ChargingControlAction.Pause, result.Action);
    }

    [Fact]
    public void Connected_SurplusExactlyAtResumeThreshold_StartsCharging()
    {
        // Resume threshold is exactly 1580W (1380 + 200). At the threshold we start.
        // 1580/230 = 6.86A -> floored to 6A.
        var result = Controller.Decide(Input(EvChargerStatus.Preparing, 1580, 0, Stopped, hasControl: true));

        Assert.Equal(ChargingControlAction.Charge, result.Action);
        Assert.Equal(6, result.TargetSettings!.ChargeCurrentAmps);
    }
}
