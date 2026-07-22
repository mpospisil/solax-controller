using Solax.Core.Enums;
using Solax.Core.Models;

namespace Solax.Core.Tests.Models;

public class EnergyStateTests
{
    [Fact]
    public void FromRawRegisters_MapsUnsignedBatterySoc()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var state = EnergyState.FromRawRegisters(
            timestamp,
            batterySocRaw: 42,
            batteryPowerRaw: 0,
            pvPowerDc1Raw: 0,
            pvPowerDc2Raw: 0,
            gridPowerRRaw: 0,
            gridPowerSRaw: 0,
            gridPowerTRaw: 0,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: 0);

        Assert.Equal(42, state.BatterySocPercent);
        Assert.Equal(timestamp, state.Timestamp);
    }

    [Theory]
    [InlineData((ushort)1500, 1500)] // charging
    [InlineData(unchecked((ushort)(short)-1500), -1500)] // discharging
    public void FromRawRegisters_MapsSignedBatteryPower_PositiveIsCharging(ushort raw, double expectedWatts)
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: raw,
            pvPowerDc1Raw: 0,
            pvPowerDc2Raw: 0,
            gridPowerRRaw: 0,
            gridPowerSRaw: 0,
            gridPowerTRaw: 0,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: 0);

        Assert.Equal(expectedWatts, state.BatteryPowerWatts);
    }

    [Theory]
    // SolaX reports the grid meter as positive = export; this model negates it to positive = import.
    [InlineData((ushort)1500, -1500)] // raw +1500 export -> -1500 (exporting)
    [InlineData(unchecked((ushort)(short)-1500), 1500)] // raw -1500 import -> +1500 (importing)
    public void FromRawRegisters_NegatesGridPower_PositiveIsImporting(ushort raw, double expectedWatts)
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: 0,
            pvPowerDc1Raw: 0,
            pvPowerDc2Raw: 0,
            gridPowerRRaw: raw,
            gridPowerSRaw: 0,
            gridPowerTRaw: 0,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: 0);

        Assert.Equal(expectedWatts, state.GridPowerWatts);
    }

    [Fact]
    public void FromRawRegisters_MapsUnsignedEvChargerPower()
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: 0,
            pvPowerDc1Raw: 0,
            pvPowerDc2Raw: 0,
            gridPowerRRaw: 0,
            gridPowerSRaw: 0,
            gridPowerTRaw: 0,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: 7000);

        Assert.Equal(7000, state.EvChargerPowerWatts);
    }

    [Fact]
    public void FromRawRegisters_SumsSolarPowerAcrossBothMpptTrackers()
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: 0,
            pvPowerDc1Raw: 300,
            pvPowerDc2Raw: 450,
            gridPowerRRaw: 0,
            gridPowerSRaw: 0,
            gridPowerTRaw: 0,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: 0);

        Assert.Equal(750, state.SolarPowerWatts);
    }

    [Fact]
    public void FromRawRegisters_SumsGridPowerAcrossAllThreePhases_ThenNegates()
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: 0,
            pvPowerDc1Raw: 0,
            pvPowerDc2Raw: 0,
            gridPowerRRaw: 500,
            gridPowerSRaw: unchecked((ushort)(short)-200),
            gridPowerTRaw: 300,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: 0);

        // Raw phases sum to +600 (net export); negated to the import-positive convention -> -600.
        Assert.Equal(-600, state.GridPowerWatts);
    }

    [Fact]
    public void OtherLoadsPowerWatts_IsHouseholdBaseLoad_ExcludingPvEvAndBattery()
    {
        // Solar 7267W, exporting 7016W to grid (import convention -> Grid = -7016), battery idle,
        // no EV: household base load = 7267 - 7016 = 251W (the residual seen in real telemetry).
        var state = new EnergyState(
            DateTimeOffset.UtcNow,
            BatterySocPercent: 100,
            BatteryPowerWatts: 0,
            SolarPowerWatts: 7267,
            GridPowerWatts: -7016,
            EvChargerStatus: EvChargerStatus.Available,
            EvChargerPowerWatts: 0);

        Assert.Equal(251, state.OtherLoadsPowerWatts);
    }

    [Fact]
    public void FromRawRegisters_MapsEvChargerStatus()
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: 0,
            pvPowerDc1Raw: 0,
            pvPowerDc2Raw: 0,
            gridPowerRRaw: 0,
            gridPowerSRaw: 0,
            gridPowerTRaw: 0,
            evChargerStatusRaw: 2,
            evChargerPowerRaw: 0);

        Assert.Equal(EvChargerStatus.Charging, state.EvChargerStatus);
    }

    [Fact]
    public void AvailableSolarPowerWatts_IsSolarMinusEvAndBatteryCharging()
    {
        // 3000W solar, EV drawing 1400W, battery charging 500W: 3000 - 1400 - 500 = 1100W left.
        var state = new EnergyState(
            DateTimeOffset.UtcNow,
            BatterySocPercent: 50,
            BatteryPowerWatts: 500,
            SolarPowerWatts: 3000,
            GridPowerWatts: -1100,
            EvChargerStatus: EvChargerStatus.Charging,
            EvChargerPowerWatts: 1400);

        Assert.Equal(1100, state.AvailableSolarPowerWatts);
    }

    [Fact]
    public void AvailableSolarPowerWatts_BatteryDischargingDoesNotAddBack()
    {
        // No solar, EV drawing 1400W, battery discharging 500W to help cover it: discharging
        // isn't "charging", so it shouldn't offset the result back toward zero.
        var state = new EnergyState(
            DateTimeOffset.UtcNow,
            BatterySocPercent: 50,
            BatteryPowerWatts: -500,
            SolarPowerWatts: 0,
            GridPowerWatts: 900,
            EvChargerStatus: EvChargerStatus.Charging,
            EvChargerPowerWatts: 1400);

        Assert.Equal(-1400, state.AvailableSolarPowerWatts);
    }
}
