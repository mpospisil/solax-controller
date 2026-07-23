namespace Solax.Worker.Configuration;

/// <summary>Which signal drives EV charge control.</summary>
public enum ChargeControlStrategy
{
    /// <summary>Charge from the Solcast solar forecast (predicted PV minus Other Loads).</summary>
    Forecast,

    /// <summary>Charge from live PV surplus, only while the home battery is full (SOC gate).</summary>
    LiveSolar,
}

/// <summary>
/// Configuration for EV charge control. Bound from the <c>"ChargeControl"</c> section. Disabled by
/// default: this feature writes to the charger hardware, and the control register addresses must be
/// verified against your device before it is safe to enable.
/// </summary>
public sealed class ChargeControlOptions
{
    public const string SectionName = "ChargeControl";

    /// <summary>Master on/off switch. Off by default (writes to hardware — verify registers first).</summary>
    public bool Enabled { get; init; }

    /// <summary>Which control strategy to use: <see cref="ChargeControlStrategy.Forecast"/> (default) or LiveSolar.</summary>
    public ChargeControlStrategy Strategy { get; init; } = ChargeControlStrategy.Forecast;

    /// <summary>
    /// When true (and <see cref="Enabled"/>), the control loop runs and logs exactly what it would
    /// write (mode and the encoded current-register value) but performs no Modbus writes. Use it to
    /// validate the setpoints against your charger before letting it write for real.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>Voltage used to convert between watts and the charger's amp setpoint.</summary>
    public double NominalVoltage { get; init; } = 230;

    /// <summary>
    /// Number of phases the charger charges on. 1 for single-phase, 3 for three-phase (e.g. X3-HAC).
    /// One amp = NominalVoltage × Phases watts, so the 6 A minimum is ~1.4 kW single-phase but ~4.2 kW
    /// three-phase. Set this to match your charger or the setpoint and minimum floor will be wrong.
    /// </summary>
    public int Phases { get; init; } = 1;

    /// <summary>Minimum viable charging current (below which charging is paused).</summary>
    public int MinChargingCurrentAmps { get; init; } = 6;

    /// <summary>Maximum charging current the charger accepts (setpoint is clamped to this).</summary>
    public int MaxChargingCurrentAmps { get; init; } = 20;

    /// <summary>Granularity of the current setpoint the hardware accepts, in amps.</summary>
    public int CurrentStepAmps { get; init; } = 1;

    /// <summary>
    /// Extra surplus (watts) required above the minimum before charging (re)starts, so a surplus
    /// hovering near the minimum doesn't flap the charger on and off.
    /// </summary>
    public double ResumeHysteresisWatts { get; init; } = 200;

    /// <summary>
    /// LiveSolar strategy only: battery SOC (%) at or above which live-solar charging engages.
    /// </summary>
    public double BatteryFullSocPercent { get; init; } = 95;

    /// <summary>
    /// LiveSolar strategy only: once charging, the SOC (%) it must fall below to disengage the gate.
    /// Sits below <see cref="BatteryFullSocPercent"/> to form a hysteresis band (the car drawing solar
    /// can dip the battery slightly). Must not exceed <see cref="BatteryFullSocPercent"/>.
    /// </summary>
    public double BatteryReleaseSocPercent { get; init; } = 90;
}
