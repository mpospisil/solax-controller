using Solax.Core.Models;

namespace Solax.Core.Interfaces;

public interface IChargingStrategy
{
    ChargingRecommendation Evaluate(EnergyState state);
}
