using Solax.Core.Enums;
using Solax.Core.Models;
using Solax.Core.Strategies;

namespace Solax.Core.Tests.Strategies;

public class SolarSurplusChargingStrategyTests
{
    // 230V nominal, 6-20A range matching a 4.6kW single-phase X1-HAC.
    private static readonly SolarSurplusChargingStrategy Strategy = new(
        nominalVoltage: 230,
        minChargingCurrentAmps: 6,
        maxChargingCurrentAmps: 20);

    private static EnergyState StateWith(double evChargerPowerWatts, double gridPowerWatts) =>
        new(
            DateTimeOffset.UtcNow,
            BatterySocPercent: 50,
            BatteryPowerWatts: 0,
            PvPowerWatts: 0,
            GridPowerWatts: gridPowerWatts,
            EvChargerStatus: EvChargerStatus.Charging,
            EvChargerPowerWatts: evChargerPowerWatts);

    [Fact]
    public void Evaluate_ExportingToGrid_RecommendsMoreThanCurrentEvPower()
    {
        // EV drawing 1400W (~6A), but exporting 1000W to the grid: there's clearly more to give.
        var state = StateWith(evChargerPowerWatts: 1400, gridPowerWatts: -1000);

        var result = Strategy.Evaluate(state);

        Assert.Equal(2400, result.SurplusPowerWatts);
        Assert.True(result.IsSurplusAvailable);
        Assert.Equal(2400.0 / 230.0, result.RecommendedChargingCurrentAmps, precision: 3);
    }

    [Fact]
    public void Evaluate_ImportingMoreThanEvIsUsing_RecommendsNoSurplus()
    {
        // EV drawing 1400W but the house is importing 2000W overall: EV is oversubscribed.
        var state = StateWith(evChargerPowerWatts: 1400, gridPowerWatts: 2000);

        var result = Strategy.Evaluate(state);

        Assert.Equal(-600, result.SurplusPowerWatts);
        Assert.False(result.IsSurplusAvailable);
        Assert.Equal(0, result.RecommendedChargingCurrentAmps);
    }

    [Fact]
    public void Evaluate_SurplusBelowMinimumViableCurrent_RecommendsNoSurplus()
    {
        // 1A worth of surplus (230W) is below the 6A minimum an EVSE can actually use.
        var state = StateWith(evChargerPowerWatts: 1000, gridPowerWatts: 770);

        var result = Strategy.Evaluate(state);

        Assert.Equal(230, result.SurplusPowerWatts);
        Assert.False(result.IsSurplusAvailable);
        Assert.Equal(0, result.RecommendedChargingCurrentAmps);
    }

    [Fact]
    public void Evaluate_SurplusExceedsChargerMax_ClampsToMaxCurrent()
    {
        // Exporting a huge amount: far more than the 20A/4.6kW charger can take.
        var state = StateWith(evChargerPowerWatts: 1000, gridPowerWatts: -9000);

        var result = Strategy.Evaluate(state);

        Assert.Equal(10000, result.SurplusPowerWatts);
        Assert.True(result.IsSurplusAvailable);
        Assert.Equal(20, result.RecommendedChargingCurrentAmps);
    }

    [Fact]
    public void Evaluate_ExactlyAtMinimumCurrent_IsAvailable()
    {
        // Exactly 6A worth of surplus (1380W).
        var state = StateWith(evChargerPowerWatts: 1380, gridPowerWatts: 0);

        var result = Strategy.Evaluate(state);

        Assert.True(result.IsSurplusAvailable);
        Assert.Equal(6, result.RecommendedChargingCurrentAmps, precision: 3);
    }
}
