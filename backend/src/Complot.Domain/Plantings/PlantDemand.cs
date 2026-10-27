using Complot.Domain.Catalog;

namespace Complot.Domain.Plantings;

public enum DemandStatus
{
    /// <summary>The stock counted in pieces covers the plants.</summary>
    Enough,

    /// <summary>Fewer pieces in stock than plants needed.</summary>
    Short,

    /// <summary>Stock in units that cannot be counted (packets, or grams without seeds per gram), or plants that cannot be counted (sown broadcast).</summary>
    Unknown,

    /// <summary>No usable stock at all.</summary>
    NoStock,
}

/// <summary>An amount of stock in one unit.</summary>
public sealed record StockAmount(decimal Quantity, string Unit);

/// <summary>
/// What one variety needs in a period: the plants of its plantings, the plantings whose number of plants is unknown,
/// the usable stock in pieces (null when none can be counted) and any stock in other units. The budget buys whole
/// units of the cheapest offer: <see cref="CostWithoutStock"/> for every plant, <see cref="CostWithStock"/> only for the
/// plants the stock does not cover. Null when no offer has a price per piece, or the stock cannot be counted.
/// </summary>
public sealed record VarietyDemand(
    Guid VarietyId, int Plants, int Plantings, int Uncounted, decimal? Stock, IReadOnlyList<StockAmount> OtherStock,
    DemandStatus Status, IReadOnlyList<Guid> BedIds, decimal? CostWithoutStock = null, decimal? CostWithStock = null);

/// <summary>Plants needed for the plantings started in a period, compared with the stock, and what buying them costs.</summary>
public static class PlantDemand
{
    /// <summary>
    /// Every planting sown (or planted, when bought as plants) from <paramref name="from"/> to <paramref name="to"/>
    /// counts, per variety. A lot is usable unless it expired before the period starts. Stock is shared by all
    /// sites, and lots are not reduced by earlier plantings: the comparison is with the stock as recorded.
    /// </summary>
    public static IReadOnlyList<VarietyDemand> Calculate(
        IEnumerable<Planting> plantings, IEnumerable<InventoryLot> lots, DateOnly from, DateOnly to, IEnumerable<VarietySupplier>? offers = null)
    {
        var usable = lots.Where(l => l.StatusOn(from) != LotStatus.Expired).ToLookup(l => l.VarietyId);
        var offered = (offers ?? []).ToLookup(o => o.VarietyId);
        return plantings
            .Where(p => p.SowOn >= from && p.SowOn <= to)
            .GroupBy(p => p.VarietyId)
            .Select(g => Demand(g.ToList(), usable[g.Key].ToList(), offered[g.Key].ToList()))
            .OrderByDescending(d => d.Status == DemandStatus.Short)
            .ThenByDescending(d => d.Plants)
            .ToList();
    }

    /// <summary>The cheapest way to buy <paramref name="pieces"/>: whole units of one offer. Null when no offer can be counted.</summary>
    public static decimal? Cost(decimal pieces, IEnumerable<VarietySupplier> offers, decimal? seedsPerGram) =>
        pieces <= 0 ? 0 : offers
            .Select(o => o.PiecesWith(seedsPerGram) is > 0 and var per ? Math.Ceiling(pieces / per) * o.Price : (decimal?)null)
            .Min();

    private static VarietyDemand Demand(List<Planting> plantings, List<InventoryLot> lots, List<VarietySupplier> offers)
    {
        var variety = plantings[0].Variety;
        var counts = plantings.Select(p => p.PlantCount()).ToList();
        var plants = counts.Sum(c => c ?? 0);
        var uncounted = counts.Count(c => c is null);
        var pieces = lots.Select(l => (Lot: l, Pieces: l.PiecesWith(variety.SeedsPerGram))).ToList();
        var counted = pieces.Where(x => x.Pieces is not null).ToList();
        decimal? stock = counted.Count > 0 ? counted.Sum(x => x.Pieces!.Value) : null;
        var other = pieces.Where(x => x.Pieces is null)
            .GroupBy(x => x.Lot.Unit.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(u => new StockAmount(u.Sum(x => x.Lot.Quantity), u.First().Lot.Unit.Trim()))
            .ToList();

        var status = lots.Count == 0 ? DemandStatus.NoStock
            : stock is { } s && s < plants ? DemandStatus.Short
            : stock is null || uncounted > 0 ? DemandStatus.Unknown
            : DemandStatus.Enough;
        var beds = plantings.Select(p => p.BedId).Distinct().ToList();
        var demand = new VarietyDemand(plantings[0].VarietyId, plants, plantings.Count, uncounted, stock, other, status, beds);
        // Broadcast sowings have no number of plants to price.
        if (plants == 0) return demand;
        var missing = plants - (stock ?? 0);
        return demand with
        {
            CostWithoutStock = Cost(plants, offers, variety.SeedsPerGram),
            CostWithStock = missing <= 0 ? 0 : other.Count == 0 ? Cost(missing, offers, variety.SeedsPerGram) : null,
        };
    }
}
