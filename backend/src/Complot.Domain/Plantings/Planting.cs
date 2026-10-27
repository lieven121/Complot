using Complot.Domain.Catalog;
using Complot.Domain.Common;
using Complot.Domain.Garden;

namespace Complot.Domain.Plantings;

/// <summary>
/// A crop in (part of) a bed. Dates are calculated from the variety when planned and can be adjusted afterwards.
/// For perennials <see cref="ClearOn"/> may stay empty (stays until removed). In a strips bed the crop takes a
/// <see cref="Share"/>; in a spots bed it is <see cref="Count"/> plants around (<see cref="SpotX"/>, <see cref="SpotY"/>), in a
/// circle or, with <see cref="SpotColumns"/>, in a block that many plants wide.
/// </summary>
public sealed class Planting : Entity
{
    /// <summary>Area per plant in a spot when the variety has no spacing (30 × 30 cm).</summary>
    public const double DefaultSpotAreaM2 = 0.09;

    public Guid BedId { get; set; }
    public Bed Bed { get; set; } = null!;
    public Guid VarietyId { get; set; }
    public PlantVariety Variety { get; set; } = null!;

    /// <summary>Part of the bed used, 0.1–1 (intercropping / succession). Ignored in a spots bed.</summary>
    public double Share { get; set; } = 1;

    /// <summary>Number of plants in the spot (spots beds only).</summary>
    public int? Count { get; set; }

    /// <summary>Centre of the spot in metres from the bed's top-left corner (spots beds only).</summary>
    public double? SpotX { get; set; }

    public double? SpotY { get; set; }

    /// <summary>Plants across a block (1 = a single row); null = a round cluster (spots beds only).</summary>
    public int? SpotColumns { get; set; }

    /// <summary>A block's rows run north–south instead of east–west.</summary>
    public bool SpotVertical { get; set; }

    public DateOnly SowOn { get; set; }

    /// <summary>Set when sown indoors first; the bed is occupied from this date.</summary>
    public DateOnly? PlantOutOn { get; set; }

    public DateOnly? HarvestFrom { get; set; }
    public DateOnly? HarvestTo { get; set; }
    public DateOnly? ClearOn { get; set; }
    public string? Notes { get; set; }

    /// <summary>First day the crop is in the bed.</summary>
    public DateOnly StartInBed => PlantOutOn ?? SowOn;

    /// <summary>Last day the crop is in the bed; <see cref="DateOnly.MaxValue"/> when it stays.</summary>
    public DateOnly EndInBed => ClearOn ?? DateOnly.MaxValue;

    public bool OverlapsInBed(Planting other) => StartInBed <= other.EndInBed && other.StartInBed <= EndInBed;

    /// <summary>Area one plant takes from the variety spacing, in m²; null when sown broadcast.</summary>
    private double? AreaPerPlant() =>
        Variety.SpacingInRowCm is > 0 and var row && Variety.SpacingBetweenRowsCm is > 0 and var between
            ? row / 100.0 * (between / 100.0)
            : null;

    /// <summary>
    /// Number of plants: the count of a spot, otherwise estimated from the bed area, share and spacing
    /// (null when sown broadcast).
    /// </summary>
    public int? PlantCount() =>
        Bed.Kind == BedKind.Spots ? Count
        : AreaPerPlant() is { } perPlant ? Math.Max(1, (int)Math.Floor(Bed.Shape.Area * Share / perPlant))
        : null;

    /// <summary>Planted area in m²: the plants of a spot, or the share of a strips bed.</summary>
    public double UsedArea() =>
        Bed.Kind == BedKind.Spots ? (Count ?? 1) * (AreaPerPlant() ?? DefaultSpotAreaM2) : Bed.Shape.Area * Share;

    /// <summary>
    /// Whether the spot fields fit the kind of bed: a spots bed needs a count and a position inside the bed, a strips
    /// bed has neither. Returns the field and error code of the first problem.
    /// </summary>
    public (string Field, string Code)? SpotProblem()
    {
        if (Bed.Kind == BedKind.Strips)
            return Count is null && SpotX is null && SpotY is null && SpotColumns is null ? null : ("count", "notSpotBed");
        if (Count is not > 0) return ("count", "notEmpty");
        if (SpotColumns is { } columns && (columns < 1 || columns > Count)) return ("spotColumns", "columnsTooMany");
        if (SpotX is not { } x || SpotY is not { } y) return ("spotX", "notEmpty");
        return x >= 0 && x <= Bed.Shape.Width && y >= 0 && y <= Bed.Shape.Height ? null : ("spotX", "spotOutside");
    }

    public void ApplyDates(PlantingDates dates)
    {
        SowOn = dates.SowOn;
        PlantOutOn = dates.PlantOutOn;
        HarvestFrom = dates.HarvestFrom;
        HarvestTo = dates.HarvestTo;
        ClearOn = dates.ClearOn;
    }
}

public sealed record PlantingDates(DateOnly SowOn, DateOnly? PlantOutOn, DateOnly? HarvestFrom, DateOnly? HarvestTo, DateOnly? ClearOn);
