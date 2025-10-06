namespace Purchases.App.Rates;

public interface IExchangeRateClient
{
    Task<(DateTime rateDate, decimal rate)> GetRateAsync(string currency, DateTime asOfDate, CancellationToken ct = default);
}
