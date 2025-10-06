using LiteDB;
using Purchases.App.Models;
using Serilog;

namespace Purchases.App.Persistence;

public sealed class LiteDbPurchaseRepository : IPurchaseRepository
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Purchase> _col;

    public LiteDbPurchaseRepository(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _db = new LiteDatabase(dbPath);
        _col = _db.GetCollection<Purchase>("purchases");
        _col.EnsureIndex(x => x.Id, unique: true);
        Log.Information("LiteDB initialized at {DbPath}", dbPath);
    }

    public Task<Guid> AddAsync(Purchase p, CancellationToken ct = default)
    {
        _col.Insert(p);
        Log.Information("Stored purchase {Id} ({Desc})", p.Id, p.Description);
        return Task.FromResult(p.Id);
    }

    public Task<Purchase?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = _col.FindById(id);
        return Task.FromResult(p);
    }

    public void Dispose() => _db?.Dispose();
}

