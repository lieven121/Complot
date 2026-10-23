using Complot.Domain.Common;

namespace Complot.Domain.Catalog;

/// <summary>A variety (cultivar) with all growing metadata. Month windows may wrap around the new year.</summary>
public sealed class PlantVariety : Entity
{
    public Guid CategoryId { get; set; }
    public PlantCategory Category { get; set; } = null!;

    /// <summary>Overrides the category's default family (e.g. spinach in the leafy greens category).</summary>
    public Guid? FamilyId { get; set; }
    public PlantFamily? Family { get; set; }

    public required string Name { get; set; }
    public string? Cultivar { get; set; }
    public string? LatinName { get; set; }
    public Life Life { get; set; }

    public decimal? SowDepthCm { get; set; }
    public int? SpacingInRowCm { get; set; }
    public int? SpacingBetweenRowsCm { get; set; }
    public int? HeightCm { get; set; }
    public List<SunExposure> Sun { get; set; } = [];

    public MonthWindow? SowIndoor { get; set; }
    public int? WeeksIndoor { get; set; }
    public MonthWindow? SowOutdoor { get; set; }
    public MonthWindow? PlantOut { get; set; }
    public required MonthWindow Harvest { get; set; }

    public DayRange? Germination { get; set; }
    public int? DaysToFirstHarvest { get; set; }
    public int? HarvestWeeks { get; set; }

    /// <summary>How long the crop may stay in one bed, in years. Null = unlimited (trees, shrubs).</summary>
    public int? MaxYearsInBed { get; set; }

    public string? Notes { get; set; }

    public List<YearlyAction> Actions { get; set; } = [];
    public List<VarietySupplier> Suppliers { get; set; } = [];

    public PlantFamily? EffectiveFamily => Family ?? Category.DefaultFamily;

    public int RotationRestYears => Category.RotationExempt ? 0 : EffectiveFamily?.RotationRestYears ?? 0;
}

public sealed class VarietySupplier
{
    public Guid VarietyId { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public decimal Price { get; set; }
    public string? Unit { get; set; }
    public string? Url { get; set; }
}
