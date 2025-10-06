using Purchases.App.Rates;
using Serilog;
using System.Net;
using System.Net.Http.Json;

namespace Purchases.App.Rates;

public class TreasuryExchangeRateClient(HttpClient http, IConfiguration cfg) : IExchangeRateClient
{
    public async Task<(DateTime rateDate, decimal rate)> GetRateAsync(string currency, DateTime asOfDate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter code.");

        currency = currency.ToUpperInvariant();
        var sixMonthsAgo = asOfDate.AddMonths(-6).Date;

        var baseUrl = cfg["Rates:BaseUrl"]!;
        var filter =
            $"currency={currency}&record_date:lte={asOfDate:yyyy-MM-dd}&record_date:gte={sixMonthsAgo:yyyy-MM-dd}";
        var url = $"{baseUrl}?sort=-record_date&fields=record_date,exchange_rate,currency&filter={Uri.EscapeDataString(filter)}&page[number]=1&page[size]=1";

        Log.Information("Fetching rate for {Currency} as of {AsOf} (window start {Start})", currency, asOfDate, sixMonthsAgo);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var resp = await http.SendAsync(req, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("Exchange rate endpoint not found (404).");

        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<TreasuryResponse>(cancellationToken: ct);
        var item = payload?.Data?.FirstOrDefault();
        if (item == null)
            throw new InvalidOperationException($"No exchange rate found for {currency} within 6 months of {asOfDate:yyyy-MM-dd}.");

        var rateDate = DateTime.Parse(item.record_date);
        if (!decimal.TryParse(item.exchange_rate, out var rate) || rate <= 0)
            throw new InvalidOperationException("Invalid rate received from provider.");

        Log.Information("Using {Currency} rate {Rate} (per USD) from {Date}", currency, rate, rateDate);
        return (rateDate, decimal.Round(rate, 6));
    }

    private sealed class TreasuryResponse
    {
        public List<Item>? Data { get; set; }
        public sealed class Item
        {
            public string record_date { get; set; } = "";
            public string currency { get; set; } = "";
            public string exchange_rate { get; set; } = "";
        }
    }
}
