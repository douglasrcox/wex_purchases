using System.ComponentModel.DataAnnotations;

namespace Purchases.App.Models;

public record CreatePurchaseRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Description { get; init; } = "";

    [Required]
    public DateTime TransactionDate { get; init; }  // ISO date expected by model binder

    [Range(0.01, double.MaxValue)]
    public decimal UsdAmount { get; init; }
}

public record PurchaseCreatedResponse(Guid Id);

public record ConvertedPurchaseResponse
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

