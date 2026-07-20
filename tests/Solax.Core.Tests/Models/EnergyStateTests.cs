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
            pvPowerRaw: 0,
            gridPowerRaw: 0,
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
            pvPowerRaw: raw,
            gridPowerRaw: raw,
            evChargerStatusRaw: 0,
            evChargerPowerRaw: raw);

        Assert.Equal(expectedWatts, state.BatteryPowerWatts);
        Assert.Equal(expectedWatts, state.PvPowerWatts);
        Assert.Equal(expectedWatts, state.GridPowerWatts);
        Assert.Equal(expectedWatts, state.EvChargerPowerWatts);
    }

    [Fact]
    public void FromRawRegisters_MapsEvChargerStatus()
    {
        var state = EnergyState.FromRawRegisters(
            DateTimeOffset.UtcNow,
            batterySocRaw: 0,
            batteryPowerRaw: 0,
            pvPowerRaw: 0,
            gridPowerRaw: 0,
            evChargerStatusRaw: 1,
            evChargerPowerRaw: 0);

        Assert.Equal(EvChargerStatus.Charging, state.EvChargerStatus);
    }
}
