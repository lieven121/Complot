using Complot.Domain.Catalog;

namespace Complot.Domain.Plantings;

public enum DemandStatus
{
    /// <summary>The stock counted in pieces covers the plants.</summary>
    Enough,

    /// <summary>Fewer pieces in stock than plants needed.</summary>
    Short,

    /// <summary>Stock only in units that are no count (grams, packets), or plants that cannot be counted (sown broadcast).</summary>
    Unknown,

    /// <summary>No usable stock at all.</summary>
    NoStock,
}

/// <summary>An amount of stock in one unit.</summary>
public sealed record StockAmount(decimal Quantity, string Unit);

/// <summary>
/// What one variety needs in a period: the plants of its plantings, the plantings whose number of plants is unknown,
/// the usable stock counted in pieces (null when there is none) and any stock in other units.
/// </summary>
public sealed record VarietyDemand(
    Guid VarietyId, int Plants, int Plantings, int Uncounted, decimal? Stock, IReadOnlyList<StockAmount> OtherStock,
    DemandStatus Status, IReadOnlyList<Guid> BedIds);

/// <summary>Plants needed for the plantings started in a period, compared with the stock.</summary>
public static class PlantDemand
{
    /// <summary>
    /// Every planting sown (or planted, when bought as plants) from <paramref name="from"/> to <paramref name="to"/>
    /// counts, per variety. A lot is usable unless it expired before the period starts. Stock is shared by all
    /// sites, and lots are not reduced by earlier plantings: the comparison is with the stock as recorded.
    /// </summary>
    public static IReadOnlyList<VarietyDemand> Calculate(IEnumerable<Planting> plantings, IEnumerable<InventoryLot> lots, DateOnly from, DateOnly to)
    {
        var usable = lots.Where(l => l.StatusOn(from) != LotStatus.Expired).ToLookup(l => l.VarietyId);
        return plantings
            .Where(p => p.SowOn >= from && p.SowOn <= to)
            .GroupBy(p => p.VarietyId)
            .Select(g => Demand(g.Key, g.ToList(), usable[g.Key].ToList()))
            .OrderByDescending(d => d.Status == DemandStatus.Short)
            .ThenByDescending(d => d.Plants)
            .ToList();
    }

    private static VarietyDemand Demand(Guid varietyId, List<Planting> plantings, List<InventoryLot> lots)
    {
        var counts = plantings.Select(p => p.PlantCount()).ToList();
        var plants = counts.Sum(c => c ?? 0);
        var uncounted = counts.Count(c => c is null);
        var pieces = lots.Where(l => l.CountsPieces).ToList();
        decimal? stock = pieces.Count > 0 ? pieces.Sum(l => l.Quantity) : null;
        var other = lots.Where(l => !l.CountsPieces)
            .GroupBy(l => l.Unit.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(u => new StockAmount(u.Sum(l => l.Quantity), u.First().Unit.Trim()))
            .ToList();

        var status = lots.Count == 0 ? DemandStatus.NoStock
            : stock is { } s && s < plants ? DemandStatus.Short
            : stock is null || uncounted > 0 ? DemandStatus.Unknown
            : DemandStatus.Enough;
        return new VarietyDemand(varietyId, plants, plantings.Count, uncounted, stock, other, status,
            plantings.Select(p => p.BedId).Distinct().ToList());
    }
}
