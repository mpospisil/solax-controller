namespace Solax.Core.Enums;

// Verified against the SolaX EV charger register map used by the
// wills106/homeassistant-solax-modbus integration (plugin_solax_ev_charger.py),
// which cross-references SolaX's own "GEN2" EV charger protocol field names.
// Both are Input Registers (Modbus function code 0x04).
public enum EvChargerRegister : ushort
{
    ChargePowerTotal = 0x000B,
    RunMode = 0x001D,
}
