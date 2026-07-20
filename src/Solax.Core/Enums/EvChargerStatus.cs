namespace Solax.Core.Enums;

public enum EvChargerStatus
{
    Unknown = 0,
    Idle = 1,
    Charging = 2,
    Fault = 3,
}

public static class EvChargerStatusMapping
{
    // TODO: verify raw status codes against the official SolaX X1/X3-HAC Modbus register map.
    public static EvChargerStatus FromRaw(ushort raw) => raw switch
    {
        0 => EvChargerStatus.Idle,
        1 => EvChargerStatus.Charging,
        2 => EvChargerStatus.Fault,
        _ => EvChargerStatus.Unknown,
    };
}
