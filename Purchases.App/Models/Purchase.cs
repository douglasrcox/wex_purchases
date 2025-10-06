namespace Purchases.App.Models;

public record Purchase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Description { get; init; } = "";
    public DateTime TransactionDate { get; init; }
    public decimal UsdAmount { get; init; }
}
