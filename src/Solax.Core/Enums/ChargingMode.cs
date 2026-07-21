namespace Solax.Core.Enums;

/// <summary>
/// Which power sources an <see cref="Interfaces.IChargingStrategy"/> is allowed to consider
/// when recommending an EV charging current. Neither mode ever recommends drawing from the
/// grid; they differ only in whether the battery's current charging allocation can be
/// redirected to the EV.
/// </summary>
public enum ChargingMode
{
    /// <summary>
    /// Only excess solar production (after the battery takes its current charging share) is
    /// considered. The battery is never touched -- neither its charging nor its discharging.
    /// </summary>
    SolarOnly,

    /// <summary>
    /// Solar production and the battery's current charging allocation are both considered
    /// available to the EV (the EV can outbid battery charging for surplus power), while still
    /// targeting zero grid exchange.
    /// </summary>
    SolarAndBattery,
}
