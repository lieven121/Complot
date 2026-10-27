using Complot.Domain.Catalog;
using Complot.Domain.Plantings;
using static Complot.Domain.Tests.Plantings.PlantingTestData;

namespace Complot.Domain.Tests.Plantings;

public sealed class PlantDemandTests
{
    private static readonly DateOnly From = new(2026, 5, 1);
    private static readonly DateOnly To = new(2026, 5, 31);

    private static InventoryLot Lot(PlantVariety variety, decimal quantity, string unit, DateOnly? bestBefore = null) =>
        new() { VarietyId = variety.Id, Quantity = quantity, Unit = unit, BestBefore = bestBefore };

    private static VarietyDemand Single(IEnumerable<Planting> plantings, params InventoryLot[] lots) =>
        Assert.Single(PlantDemand.Calculate(plantings, lots, From, To));

    [Fact]
    public void Counts_the_plants_sown_in_the_period_and_compares_them_with_stock_in_pieces()
    {
        var bed = SpotsBed("M1");
        var demand = Single(
            [Spot(bed, CherryTomato, new(2026, 5, 3), 4), Spot(bed, CherryTomato, new(2026, 5, 20), 3), Spot(bed, CherryTomato, new(2026, 6, 2), 9)],
            Lot(CherryTomato, 6, "zaden"), Lot(CherryTomato, 1, "Plantjes"));

        Assert.Equal((7, 2, 7m, DemandStatus.Enough), (demand.Plants, demand.Plantings, demand.Stock!.Value, demand.Status));
        Assert.Equal([bed.Id], demand.BedIds);
    }

    [Fact]
    public void Too_few_pieces_is_short()
    {
        var demand = Single([Spot(SpotsBed("M1"), CherryTomato, From, 10)], Lot(CherryTomato, 6, "st"));
        Assert.Equal(DemandStatus.Short, demand.Status);
    }

    [Fact]
    public void Stock_in_grams_cannot_be_compared()
    {
        var demand = Single([Spot(SpotsBed("M1"), CherryTomato, From, 10)], Lot(CherryTomato, 2, "g"), Lot(CherryTomato, 1.5m, "G"));
        Assert.Equal((DemandStatus.Unknown, (decimal?)null), (demand.Status, demand.Stock));
        Assert.Equal(new StockAmount(3.5m, "g"), Assert.Single(demand.OtherStock));
    }

    [Fact]
    public void Expired_lots_do_not_count()
    {
        var demand = Single([Spot(SpotsBed("M1"), CherryTomato, From, 2)], Lot(CherryTomato, 50, "zaden", From.AddDays(-1)));
        Assert.Equal(DemandStatus.NoStock, demand.Status);
    }

    [Fact]
    public void A_strips_bed_counts_plants_from_its_spacing_and_broadcast_sowing_stays_uncounted()
    {
        var demand = PlantDemand.Calculate([Plant(Bed("A1", 0), CherryTomato, From), Plant(Bed("A2", 2), Onion, From)], [], From, To);

        Assert.Equal(13, demand.Single(d => d.VarietyId == CherryTomato.Id).Plants); // 4.8 m² / (0.5 × 0.7)
        var onion = demand.Single(d => d.VarietyId == Onion.Id);
        Assert.Equal((0, 1, DemandStatus.NoStock), (onion.Plants, onion.Uncounted, onion.Status));
    }
}
