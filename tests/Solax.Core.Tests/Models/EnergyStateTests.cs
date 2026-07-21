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
    [InlineData((ushort)1500, 1500)] // charging / importing
    [InlineData(unchecked((ushort)(short)-1500), -1500)] // discharging / exporting
    public void FromRawRegisters_MapsSignedPowerRegisters(ushort raw, double expectedWatts)
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: raw,
            pvPowerDc1Raw: 0,
            pvPowerDc2Raw: 0,
            gridPowerRRaw: raw,
            gridPowerSRaw: 0,
            gridPowerTRaw: 0,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: 0);

        Assert.Equal(expectedWatts, state.BatteryPowerWatts);
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
    public void FromRawRegisters_SumsGridPowerAcrossAllThreePhases()
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

        Assert.Equal(600, state.GridPowerWatts);
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
}
