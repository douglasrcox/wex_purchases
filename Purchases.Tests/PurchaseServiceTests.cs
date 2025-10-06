using Moq;
using Purchases.App.Models;
using Purchases.App.Persistence;
using Purchases.App.Rates;
using Purchases.App.Services;
using Xunit;

namespace Purchases.Tests;

public class PurchaseServiceTests
{
    private readonly Mock<IPurchaseRepository> _repo = new();
    private readonly Mock<IExchangeRateClient> _rates = new();

    [Fact]
    public async Task AddAsync_Validates_And_Rounds_To_Cents()
    {
        // Arrange
        _repo.Setup(r => r.AddAsync(It.IsAny<Purchase>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(Guid.NewGuid());

        var svc = new PurchaseService(_repo.Object, _rates.Object);

        // Act
        var id = await svc.AddAsync("Coffee", new DateTime(2025, 9, 1), 2.995m);

        // Assert
        Assert.NotEqual(Guid.Empty, id);
        _repo.Verify(r => r.AddAsync(It.Is<Purchase>(p =>
            p.Description == "Coffee" &&
            p.TransactionDate == new DateTime(2025, 9, 1) &&
            p.UsdAmount == 3.00m
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_Rejects_Invalid_Description_Length()
    {
        var svc = new PurchaseService(_repo.Object, _rates.Object);

        // too long (51 chars)
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddAsync(new string('x', 51), DateTime.UtcNow, 10m));

        Assert.Contains("Description", ex.Message);
    }

    [Fact]
    public async Task AddAsync_Rejects_Non_Positive_Amount()
    {
        var svc = new PurchaseService(_repo.Object, _rates.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddAsync("Item", DateTime.UtcNow, 0m));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.AddAsync("Item", DateTime.UtcNow, -1m));
    }

    [Fact]
    public async Task GetConvertedAsync_Uses_Rate_And_Rounds_Converted_Amount()
    {
        // Arrange
        var pid = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(pid, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Purchase
             {
                 Id = pid,
                 Description = "Book",
                 TransactionDate = new DateTime(2025, 8, 15),
                 UsdAmount = 10.00m
             });

        _rates.Setup(x => x.GetRateAsync("EUR", new DateTime(2025, 8, 15), It.IsAny<CancellationToken>()))
              .ReturnsAsync((new DateTime(2025, 8, 1), 0.912345m));

        var svc = new PurchaseService(_repo.Object, _rates.Object);

        // Act
        var res = await svc.GetConvertedAsync(pid, "EUR");

        // Assert
        Assert.Equal(pid, res.Id);
        Assert.Equal("EUR", res.Currency);
        Assert.Equal(new DateTime(2025, 8, 1), res.RateDate);
        Assert.Equal(9.12m, res.ConvertedAmount); // 10.00 * 0.912345 -> 9.12345 -> 9.12
    }

    [Fact]
    public async Task GetConvertedAsync_Throws_When_Purchase_Not_Found()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Purchase?)null);

        var svc = new PurchaseService(_repo.Object, _rates.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetConvertedAsync(Guid.NewGuid(), "EUR"));
    }

    [Fact]
    public async Task GetConvertedAsync_Propagates_NoRate_As_InvalidOperation()
    {
        var pid = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(pid, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new Purchase
             {
                 Id = pid,
                 Description = "NoRate",
                 TransactionDate = new DateTime(2025, 8, 15),
                 UsdAmount = 5m
             });

        _rates.Setup(x => x.GetRateAsync("JPY", new DateTime(2025, 8, 15), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("No exchange rate"));

        var svc = new PurchaseService(_repo.Object, _rates.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.GetConvertedAsync(pid, "JPY"));
    }
}
