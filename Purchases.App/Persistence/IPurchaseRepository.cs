using Purchases.App.Models;

namespace Purchases.App.Persistence;

public interface IPurchaseRepository : IDisposable
{
    Task<Guid> AddAsync(Purchase p, CancellationToken ct = default);
    Task<Purchase?> GetAsync(Guid id, CancellationToken ct = default);
}
