namespace Solax.Core.Enums;

// TODO: placeholder addresses — verify against the official SolaX X3-HYB-G4 PRO
// Modbus Power Control Protocol document before connecting to real hardware.
public enum InverterRegister : ushort
{
    BatterySoc = 0x001C,
    BatteryPower = 0x0018,
    PvPower = 0x000A,
    GridPower = 0x0046,
}
