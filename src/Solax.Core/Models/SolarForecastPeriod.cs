namespace Solax.Core.Models;

/// <summary>
/// A single forecast interval from a solar-forecast provider: the estimated PV power over a
/// fixed-length window ending at <see cref="PeriodEnd"/>.
/// </summary>
/// <param name="PeriodEnd">
/// The (exclusive) end of the window this estimate covers. Providers such as Solcast timestamp
/// each estimate by its period end in UTC; the window the estimate applies to is
/// <c>(PeriodEnd - Period, PeriodEnd]</c>.
/// </param>
/// <param name="Period">The length of the window, e.g. 30 minutes.</param>
/// <param name="EstimatedPowerWatts">
/// The average PV production expected over the window, in watts. Kept in watts to match the rest
/// of the codebase's <c>...Watts</c> convention (providers typically report kilowatts).
/// </param>
public sealed record SolarForecastPeriod(
    DateTimeOffset PeriodEnd,
    TimeSpan Period,
    double EstimatedPowerWatts)
{
    /// <summary>The (exclusive) start of the window this estimate covers.</summary>
    public DateTimeOffset PeriodStart => PeriodEnd - Period;

    /// <summary>
    /// Whether the given instant falls within this period's window <c>(PeriodStart, PeriodEnd]</c>.
    /// </summary>
    public bool Covers(DateTimeOffset instant) => instant > PeriodStart && instant <= PeriodEnd;

    /// <summary>The energy this period represents (average power over its duration), in watt-hours.</summary>
    public double EnergyWattHours => EstimatedPowerWatts * Period.TotalHours;
}
