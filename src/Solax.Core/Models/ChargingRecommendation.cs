namespace Solax.Core.Models;

public sealed record ChargingRecommendation(
    double SurplusPowerWatts,
    double RecommendedChargingCurrentAmps,
    bool IsSurplusAvailable);
