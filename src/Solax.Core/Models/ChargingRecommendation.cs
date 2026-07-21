namespace Solax.Core.Models;

public sealed record ChargingRecommendation(
    double SurplusPowerWatts,
    double AvailableSolarPowerWatts,
    double RecommendedChargingCurrentAmps,
    bool IsSurplusAvailable);
