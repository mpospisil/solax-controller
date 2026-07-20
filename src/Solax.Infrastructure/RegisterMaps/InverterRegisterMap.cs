using Solax.Core.Enums;

namespace Solax.Infrastructure.RegisterMaps;

public static class InverterRegisterMap
{
    public static readonly RegisterDescriptor BatterySoc =
        new((ushort)InverterRegister.BatterySoc, nameof(BatterySoc), "%");

    public static readonly RegisterDescriptor BatteryPower =
        new((ushort)InverterRegister.BatteryPower, nameof(BatteryPower), "W");

    public static readonly RegisterDescriptor PvPower =
        new((ushort)InverterRegister.PvPower, nameof(PvPower), "W");

    public static readonly RegisterDescriptor GridPower =
        new((ushort)InverterRegister.GridPower, nameof(GridPower), "W");
}
