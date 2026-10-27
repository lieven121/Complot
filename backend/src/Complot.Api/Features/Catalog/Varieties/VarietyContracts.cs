using Complot.Api.Common;
using Complot.Domain.Catalog;
using Complot.Domain.Tools;

namespace Complot.Api.Features.Catalog.Varieties;

/// <summary>An offer; <see cref="PiecesPerUnit"/> is as entered, <see cref="Pieces"/> also follows from the unit ("zakje 4 g").</summary>
public sealed record VarietySupplierDto(
    Guid SupplierId, string SupplierName, bool IsOwnSource, decimal Price, string? Unit, string? Url, decimal? PiecesPerUnit = null, decimal? Pieces = null);

/// <summary>The variety needs <see cref="Quantity"/> of a tool per plant or per bed while it is in a bed.</summary>
public sealed record ToolNeedDto(Guid ToolId, NeedPer Per, decimal Quantity);

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
    IReadOnlyList<StockDto> Stock,
    IReadOnlyList<ToolNeedDto> ToolNeeds,
    PollinationDto? Pollination,
    decimal? SeedsPerGram = null);

public sealed record VarietySupplierInput(Guid SupplierId, decimal Price, string? Unit, string? Url, decimal? PiecesPerUnit = null);

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
    List<VarietySupplierInput> Suppliers,
    List<ToolNeedDto>? ToolNeeds = null,
    PollinationDto? Pollination = null,
    decimal? SeedsPerGram = null);
