namespace Solax.Worker.Configuration;

/// <summary>
/// Configuration for forecast-driven EV charge control. Bound from the <c>"ChargeControl"</c>
/// section. Disabled by default: this feature writes to the charger hardware, and the control
/// register addresses must be verified against your device before it is safe to enable.
/// </summary>
public sealed class ChargeControlOptions
{
    public const string SectionName = "ChargeControl";

    /// <summary>Master on/off switch. Off by default (writes to hardware — verify registers first).</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// When true (and <see cref="Enabled"/>), the control loop runs and logs exactly what it would
    /// write (mode and the encoded current-register value) but performs no Modbus writes. Use it to
    /// validate the setpoints against your charger before letting it write for real.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>Voltage used to convert between watts and the charger's amp setpoint.</summary>
    public double NominalVoltage { get; init; } = 230;

    /// <summary>Minimum viable charging current (below which charging is paused).</summary>
    public int MinChargingCurrentAmps { get; init; } = 6;

    /// <summary>Maximum charging current the charger accepts (setpoint is clamped to this).</summary>
    public int MaxChargingCurrentAmps { get; init; } = 20;

    /// <summary>Granularity of the current setpoint the hardware accepts, in amps.</summary>
    public int CurrentStepAmps { get; init; } = 1;

    /// <summary>
    /// Extra predicted surplus (watts) required above the minimum before charging (re)starts, so a
    /// forecast hovering near the minimum doesn't flap the charger on and off.
    /// </summary>
    public double ResumeHysteresisWatts { get; init; } = 200;
}
