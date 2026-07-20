using Solax.Core.Enums;

namespace Solax.Infrastructure.RegisterMaps;

public static class EvChargerRegisterMap
{
    public static readonly RegisterDescriptor Status =
        new((ushort)EvChargerRegister.Status, nameof(Status), "enum");

    public static readonly RegisterDescriptor Power =
        new((ushort)EvChargerRegister.Power, nameof(Power), "W");
}
