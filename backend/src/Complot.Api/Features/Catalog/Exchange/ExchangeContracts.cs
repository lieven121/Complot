using Complot.Api.Common;

namespace Complot.Api.Features.Catalog.Exchange;

/// <summary>
/// Catalog exchange format, used for JSON export/import and as the answer format of the AI prompt.
/// References are human-readable (category slug, Latin family name, supplier name) so an AI can write it.
/// Fields are deliberately lenient (strings, arrays); the importer reports what it cannot use.
/// </summary>
public sealed record CatalogExchange(List<ExchangeCategory>? Categories, List<ExchangeVariety>? Varieties);

public sealed record ExchangeCategory(
    string? Id,
    string? Name,
    string? Family,
    string? Icon,
    string? Color,
    bool? RotationExempt,
    List<string>? GoodNeighbours,
    List<string>? BadNeighbours);

public sealed record ExchangeVariety(
    string? Category,
    string? Name,
    string? Cultivar,
    string? Latin,
    string? Family,
    string? Life,
    decimal? SowDepthCm,
    int? SpacingInRowCm,
    int? SpacingBetweenRowsCm,
    int? HeightCm,
    List<string>? Sun,
    int[]? SowIndoorMonths,
    int? WeeksIndoorBeforePlanting,
    int[]? SowOutdoorMonths,
    int[]? PlantOutMonths,
    int[]? GerminationDays,
    int? DaysToFirstHarvest,
    int? HarvestWeeks,
    int[]? HarvestMonths,
    int? MaxYearsInBed,
    string? Notes,
    List<ExchangeAction>? Actions,
    List<ExchangeSupplier>? Suppliers);

public sealed record ExchangeAction(string Title, int Month, int EstimatedMinutes);

public sealed record ExchangeSupplier(string Name, decimal? Price, string? Unit, string? Url);

public enum ImportRowStatus
{
    New,
    Update,
    Error,
}

/// <summary>Outcome for one category or variety row. Errors block the row; warnings do not.</summary>
public sealed record ImportRow(
    int Index,
    string Name,
    string? Detail,
    ImportRowStatus Status,
    IReadOnlyDictionary<string, ErrorDetail[]>? Errors,
    IReadOnlyList<ErrorDetail> Warnings);

public sealed record ImportReport(bool Committed, IReadOnlyList<ImportRow> Categories, IReadOnlyList<ImportRow> Varieties);
