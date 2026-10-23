using Complot.Api.Common;
using Complot.Domain.Catalog;

namespace Complot.Api.Features.Catalog.Varieties;

public sealed record VarietySupplierDto(Guid SupplierId, string SupplierName, bool IsOwnSource, decimal Price, string? Unit, string? Url);

public sealed record StockDto(Guid LotId, decimal Quantity, string Unit, LotStatus Status);

/// <summary>A variety with everything the catalog screens show, including the computed rotation rest and stock.</summary>
public sealed record VarietyDto(
    Guid Id,
    Guid CategoryId,
    Guid? FamilyId,
    Guid? EffectiveFamilyId,
    int RotationRestYears,
    string Name,
    string? Cultivar,
    string? LatinName,
    Life Life,
    decimal? SowDepthCm,
    int? SpacingInRowCm,
    int? SpacingBetweenRowsCm,
    int? HeightCm,
    IReadOnlyList<SunExposure> Sun,
    MonthWindowDto? SowIndoor,
    int? WeeksIndoor,
    MonthWindowDto? SowOutdoor,
    MonthWindowDto? PlantOut,
    MonthWindowDto Harvest,
    DayRangeDto? Germination,
    int? DaysToFirstHarvest,
    int? HarvestWeeks,
    int? MaxYearsInBed,
    string? Notes,
    IReadOnlyList<YearlyActionDto> Actions,
    IReadOnlyList<VarietySupplierDto> Suppliers,
    IReadOnlyList<StockDto> Stock);

public sealed record VarietySupplierInput(Guid SupplierId, decimal Price, string? Unit, string? Url);

public sealed record VarietyInput(
    Guid CategoryId,
    Guid? FamilyId,
    string Name,
    string? Cultivar,
    string? LatinName,
    Life Life,
    decimal? SowDepthCm,
    int? SpacingInRowCm,
    int? SpacingBetweenRowsCm,
    int? HeightCm,
    List<SunExposure> Sun,
    MonthWindowDto? SowIndoor,
    int? WeeksIndoor,
    MonthWindowDto? SowOutdoor,
    MonthWindowDto? PlantOut,
    MonthWindowDto Harvest,
    DayRangeDto? Germination,
    int? DaysToFirstHarvest,
    int? HarvestWeeks,
    int? MaxYearsInBed,
    string? Notes,
    List<YearlyActionDto> Actions,
    List<VarietySupplierInput> Suppliers);
