using Complot.Domain.Common;

namespace Complot.Domain.Catalog;

/// <summary>A batch of seed or plant material in stock.</summary>
public sealed class InventoryLot : Entity
{
    /// <summary>A lot expiring within this many days is flagged "use soon".</summary>
    public const int UseSoonDays = 120;

    public Guid VarietyId { get; set; }
    public PlantVariety Variety { get; set; } = null!;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public decimal Quantity { get; set; }
    public required string Unit { get; set; }
    public DateOnly BoughtOn { get; set; }
    public DateOnly? BestBefore { get; set; }
    public string? Notes { get; set; }

    /// <summary>Units that count pieces (seeds, plants, bulbs…), so the quantity compares with a number of plants.</summary>
    private static readonly HashSet<string> CountUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "st", "st.", "stuk", "stuks", "x", "pcs", "piece", "pieces",
        "zaad", "zaden", "zaadjes", "seed", "seeds",
        "plant", "planten", "plantjes", "plants", "plug", "plugs", "plugplanten",
        "bol", "bollen", "bulb", "bulbs", "knol", "knollen", "tuber", "tubers", "pootgoed",
        "uitloper", "uitlopers", "runner", "runners", "stek", "stekken", "cutting", "cuttings",
    };

    /// <summary>Whether <see cref="Quantity"/> is a number of pieces rather than a weight or a packet.</summary>
    public bool CountsPieces => CountUnits.Contains(Unit.Trim());

    public LotStatus StatusOn(DateOnly today) => BestBefore switch
    {
        null => LotStatus.Ok,
        var bb when bb < today => LotStatus.Expired,
        var bb when bb < today.AddDays(UseSoonDays) => LotStatus.UseSoon,
        _ => LotStatus.Ok,
    };
}
