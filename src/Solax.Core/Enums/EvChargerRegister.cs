namespace Solax.Core.Enums;

// TODO: placeholder addresses — verify against the official SolaX X1/X3-HAC
// Modbus register map document before connecting to real hardware.
public enum EvChargerRegister : ushort
{
    Status = 0x0000,
    Power = 0x0002,
}
