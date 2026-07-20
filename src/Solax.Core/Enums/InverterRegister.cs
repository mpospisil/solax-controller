namespace Solax.Core.Enums;

// Offsets within the Input Register block (Modbus function code 0x04) of the SolaX
// X1/X3 Hybrid G4 inverter. Verified against "Energy Storage Inverter Modbus TCP&RTU
// Communication protocols" V3.21 (SolaX Power). Real-time telemetry lives in Input
// Registers; the similarly-numbered Holding Registers (function code 0x03) are
// configuration/protection parameters and mean something entirely different.
public enum InverterRegister : ushort
{
    Powerdc1 = 0x000A,
    Powerdc2 = 0x000B,
    BatteryPowerCharge1 = 0x0016,
    BatteryCapacity = 0x001C,
    GridPowerR = 0x006C,
    GridPowerS = 0x0070,
    GridPowerT = 0x0074,
}
