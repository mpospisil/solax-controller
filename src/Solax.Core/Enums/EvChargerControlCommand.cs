namespace Solax.Core.Enums;

// Values of the SolaX EV charger's Control Command holding register (0x627), which starts/stops a
// charging session independently of the use-mode register. Sourced from the SolaX charger protocol
// via the wills106 homeassistant-solax-modbus map.
//
// !! This register is written but never read back by us (it is a command, not a persistent setting),
// and it has NOT been verified against the hardware. Validate with ChargeControl:DryRun before
// enabling real writes.
public enum EvChargerControlCommand : ushort
{
    NoCommand = 0,
    Available = 1,
    Unavailable = 2,
    StopCharging = 3,
    StartCharging = 4,
    Reserve = 5,
    CancelReservation = 6,
}
