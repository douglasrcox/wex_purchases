using Microsoft.Extensions.Caching.Memory;
using Purchases.App.Rates;
using Serilog;

namespace Purchases.App.Rates;

public sealed class CachingExchangeRateClient(IExchangeRateClient inner, IMemoryCache cache, ILogger<CachingExchangeRateClient> log) : IExchangeRateClient
{
    public async Task<(DateTime rateDate, decimal rate)> GetRateAsync(string currency, DateTime asOfDate, CancellationToken ct = default)
    {
        currency = currency?.ToUpperInvariant() ?? throw new ArgumentNullException(nameof(currency));

        // Month bucket cache key for good hit rate with stable historicals
        var bucket = new DateTime(asOfDate.Year, asOfDate.Month, 1);
        var key = $"exrate::{currency}::{bucket:yyyy-MM}";

        if (cache.TryGetValue<(DateTime rateDate, decimal rate)>(key, out var cached))
        {
            log.LogInformation("Rate cache hit {Key}", key);
            return cached;
        }

        var value = await inner.GetRateAsync(currency, asOfDate, ct);

        cache.Set(key, value, new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetAbsoluteExpiration(TimeSpan.FromHours(24)));

        log.LogInformation("Rate cache set {Key}", key);
        return value;
    }
}
