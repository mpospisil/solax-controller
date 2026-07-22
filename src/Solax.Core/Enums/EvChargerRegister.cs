namespace Solax.Core.Enums;

// Register map for the SolaX "GEN2" EV charger, cross-referenced with the
// wills106/homeassistant-solax-modbus integration (plugin_solax_ev_charger.py).
public enum EvChargerRegister : ushort
{
    // --- Telemetry: Input Registers (Modbus function code 0x04), read-only. Verified. ---
    ChargePowerTotal = 0x000B,
    RunMode = 0x001D,

    // --- Control: Holding Registers (function codes 0x03/0x06/0x10), writable. ---
    // !! UNVERIFIED PLACEHOLDER ADDRESSES. Confirm both the addresses and their value encodings
    // against your specific charger / the SolaX GEN2 protocol BEFORE enabling control writes
    // (ChargeControl:Enabled, which defaults to false). Writing to a wrong address or with a wrong
    // encoding could misconfigure the charger. See issue #10.
    ChargerUseMode = 0x0060,        // 0=Stop, 1=Fast, 2=Eco, 3=Green (see EvChargerMode)
    ChargeCurrentSetpoint = 0x0061, // target charging current, in whole amps
}
