using Solax.Core.Models;

namespace Solax.Core.Interfaces;

public interface IEnergyStateReader
{
    Task<EnergyState> ReadAsync(CancellationToken cancellationToken = default);
}
