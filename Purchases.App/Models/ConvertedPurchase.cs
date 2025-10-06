namespace Purchases.App.Models;

public record ConvertedPurchase
{
    public Guid Id { get; init; }
    public string Description { get; init; } = "";
    public DateTime TransactionDate { get; init; }
    public decimal UsdAmount { get; init; }
    public string Currency { get; init; } = "";
    public DateTime RateDate { get; init; }
    public decimal ExchangeRate { get; init; }
    public decimal ConvertedAmount { get; init; }
}
