using Purchases.App.Models;
using Purchases.App.Persistence;
using Purchases.App.Rates;
using Serilog;

namespace Purchases.App.Services;

public class PurchaseService(IPurchaseRepository repo, IExchangeRateClient rates)
{
    public async Task<Guid> AddAsync(string description, DateTime transactionDate, decimal usdAmount, CancellationToken ct = default)
    {
        description = (description ?? "").Trim();
        if (description.Length is 0 or > 50)
            throw new ArgumentException("Description must be 1–50 characters.");

        if (usdAmount <= 0)
            throw new ArgumentException("Purchase amount must be positive.");

        usdAmount = decimal.Round(usdAmount, 2, MidpointRounding.AwayFromZero);

        var p = new Purchase
        {
            Description = description,
            TransactionDate = transactionDate.Date,
            UsdAmount = usdAmount
        };

        var id = await repo.AddAsync(p, ct);
        Log.Information("Added purchase {Id} on {Date} for {Amt}", id, p.TransactionDate, p.UsdAmount);
        return id;
    }

    public async Task<ConvertedPurchase> GetConvertedAsync(Guid id, string currency, CancellationToken ct = default)
    {
        var p = await repo.GetAsync(id, ct) ?? throw new KeyNotFoundException($"Purchase {id} not found.");
        var (rateDate, rate) = await rates.GetRateAsync(currency, p.TransactionDate, ct);

        var converted = decimal.Round(p.UsdAmount * rate, 2, MidpointRounding.AwayFromZero);

        return new ConvertedPurchase
        {
            Id = p.Id,
            Description = p.Description,
            TransactionDate = p.TransactionDate,
            UsdAmount = p.UsdAmount,
            Currency = currency.ToUpperInvariant(),
            RateDate = rateDate.Date,
            ExchangeRate = decimal.Round(rate, 6),
            ConvertedAmount = converted
        };
    }
}
