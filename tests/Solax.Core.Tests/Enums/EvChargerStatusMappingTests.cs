using Solax.Core.Enums;

namespace Solax.Core.Tests.Enums;

public class EvChargerStatusMappingTests
{
    [Theory]
    [InlineData((ushort)0, EvChargerStatus.Idle)]
    [InlineData((ushort)1, EvChargerStatus.Charging)]
    [InlineData((ushort)2, EvChargerStatus.Fault)]
    [InlineData((ushort)99, EvChargerStatus.Unknown)]
    public void FromRaw_MapsKnownAndUnknownCodes(ushort raw, EvChargerStatus expected)
    {
        Assert.Equal(expected, EvChargerStatusMapping.FromRaw(raw));
    }
}
