using Complot.Domain.Catalog;

namespace Complot.Domain.Tests.Catalog;

public sealed class InventoryLotTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private static InventoryLot Lot(DateOnly? bestBefore) => new() { Unit = "g", Quantity = 1, BestBefore = bestBefore };

    [Fact]
    public void Lot_without_best_before_date_is_ok() => Assert.Equal(LotStatus.Ok, Lot(null).StatusOn(Today));

    [Fact]
    public void Lot_past_best_before_is_expired() => Assert.Equal(LotStatus.Expired, Lot(Today.AddDays(-1)).StatusOn(Today));

    [Fact]
    public void Lot_expiring_within_the_threshold_must_be_used_soon() =>
        Assert.Equal(LotStatus.UseSoon, Lot(Today.AddDays(InventoryLot.UseSoonDays - 1)).StatusOn(Today));

    [Fact]
    public void Lot_expiring_after_the_threshold_is_ok() =>
        Assert.Equal(LotStatus.Ok, Lot(Today.AddDays(InventoryLot.UseSoonDays)).StatusOn(Today));
}
