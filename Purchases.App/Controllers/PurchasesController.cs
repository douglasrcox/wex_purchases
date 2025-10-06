using Microsoft.AspNetCore.Mvc;
using Purchases.App.Models;
using Purchases.App.Services;

namespace Purchases.App.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController(PurchaseService service) : ControllerBase
{
    // POST /api/purchases
    [HttpPost]
    public async Task<ActionResult<PurchaseCreatedResponse>> Create([FromBody] CreatePurchaseRequest req, CancellationToken ct)
    {
        var id = await service.AddAsync(req.Description, req.TransactionDate, req.UsdAmount, ct);
        return CreatedAtAction(nameof(Convert), new { id, currency = "USD" }, new PurchaseCreatedResponse(id));
    }

    // GET /api/purchases/{id}/convert?currency=EUR
    [HttpGet("{id:guid}/convert")]
    public async Task<ActionResult<ConvertedPurchaseResponse>> Convert([FromRoute] Guid id, [FromQuery] string currency, CancellationToken ct)
    {
        var result = await service.GetConvertedAsync(id, currency, ct);
        return Ok(new ConvertedPurchaseResponse
        {
            Id = result.Id,
            Description = result.Description,
            TransactionDate = result.TransactionDate,
            UsdAmount = result.UsdAmount,
            Currency = result.Currency,
            RateDate = result.RateDate,
            ExchangeRate = result.ExchangeRate,
            ConvertedAmount = result.ConvertedAmount
        });
    }
}
