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

    /// <summary>The lot in pieces (seeds, plants…); null when its unit cannot be counted (see <see cref="Pieces"/>).</summary>
    public decimal? PiecesWith(decimal? seedsPerGram) => Pieces.Of(Quantity, Unit, seedsPerGram);

    public LotStatus StatusOn(DateOnly today) => BestBefore switch
    {
        null => LotStatus.Ok,
        var bb when bb < today => LotStatus.Expired,
        var bb when bb < today.AddDays(UseSoonDays) => LotStatus.UseSoon,
        _ => LotStatus.Ok,
    };
}
