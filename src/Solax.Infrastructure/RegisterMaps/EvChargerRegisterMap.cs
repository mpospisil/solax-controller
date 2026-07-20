using Solax.Core.Enums;

namespace Solax.Infrastructure.RegisterMaps;

public static class EvChargerRegisterMap
{
    public static readonly RegisterDescriptor ChargePowerTotal =
        new((ushort)EvChargerRegister.ChargePowerTotal, nameof(ChargePowerTotal), "W");

    public static readonly RegisterDescriptor RunMode =
        new((ushort)EvChargerRegister.RunMode, nameof(RunMode), "enum");
}
