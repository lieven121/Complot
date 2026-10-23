using Complot.Domain.Catalog;
using Complot.Domain.Common;
using Complot.Domain.Garden;

namespace Complot.Domain.Plantings;

/// <summary>
/// A crop in (part of) a bed. Dates are calculated from the variety when planned and can be adjusted afterwards.
/// For perennials <see cref="ClearOn"/> may stay empty (stays until removed).
/// </summary>
public sealed class Planting : Entity
{
    public Guid BedId { get; set; }
    public Bed Bed { get; set; } = null!;
    public Guid VarietyId { get; set; }
    public PlantVariety Variety { get; set; } = null!;

    /// <summary>Part of the bed used, 0.1–1 (intercropping / succession).</summary>
    public double Share { get; set; } = 1;

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

    /// <summary>Estimated number of plants from the bed area, share and spacing; null when sown broadcast.</summary>
    public int? PlantCount() =>
        Variety.SpacingInRowCm is > 0 and var row && Variety.SpacingBetweenRowsCm is > 0 and var between
            ? Math.Max(1, (int)Math.Floor(Bed.Shape.Area * Share / (row / 100.0 * (between / 100.0))))
            : null;

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
