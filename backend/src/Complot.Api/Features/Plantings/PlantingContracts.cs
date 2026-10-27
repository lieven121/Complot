using Complot.Domain.Plantings;

namespace Complot.Api.Features.Plantings;

public sealed record PhaseSegmentDto(PlantingPhase Phase, DateOnly From, DateOnly To);

/// <summary>
/// A planting with its phase segments for the requested period (the map just looks up the segment of a date).
/// <see cref="UsedArea"/> (m²) sizes a spot on the map.
/// </summary>
public sealed record PlantingDto(
    Guid Id, Guid BedId, Guid VarietyId, double Share, int? Count, double? SpotX, double? SpotY, int? SpotColumns, bool SpotVertical,
    DateOnly SowOn, DateOnly? PlantOutOn, DateOnly? HarvestFrom, DateOnly? HarvestTo, DateOnly? ClearOn,
    string? Notes, int? PlantCount, double UsedArea, IReadOnlyList<PhaseSegmentDto> Phases);

/// <summary>Rule finding; the UI translates <see cref="Code"/> with <see cref="Params"/>.</summary>
public sealed record PlantingWarningDto(
    Guid PlantingId, Guid BedId, WarningLevel Level, string Code, DateOnly Date, IReadOnlyDictionary<string, object?> Params);

/// <summary>
/// Plan a new planting: the dates follow from the variety and the sowing date. A spots bed needs
/// <see cref="Count"/> and the spot position (m from the bed's top-left corner) instead of a share, and optionally
/// the block it is planted in (<see cref="SpotColumns"/> plants wide; null = a round cluster).
/// </summary>
public sealed record PlanPlantingInput(
    Guid BedId, Guid VarietyId, double Share, DateOnly SowOn, bool Direct, string? Notes,
    int? Count = null, double? SpotX = null, double? SpotY = null, int? SpotColumns = null, bool SpotVertical = false);

public sealed record PlantingPreviewDto(PlantingDto Planting, IReadOnlyList<PlantingWarningDto> Warnings);

/// <summary>Adjust a planting by hand (e.g. the actual planting-out or harvest dates).</summary>
public sealed record UpdatePlantingInput(
    double Share, DateOnly SowOn, DateOnly? PlantOutOn, DateOnly? HarvestFrom, DateOnly? HarvestTo, DateOnly? ClearOn, string? Notes,
    int? Count = null, double? SpotX = null, double? SpotY = null, int? SpotColumns = null, bool SpotVertical = false);

public static class PlantingMapping
{
    public static PlantingDto ToDto(this Planting p, DateOnly from, DateOnly to) => new(
        p.Id, p.BedId, p.VarietyId, p.Share, p.Count, p.SpotX, p.SpotY, p.SpotColumns, p.SpotVertical,
        p.SowOn, p.PlantOutOn, p.HarvestFrom, p.HarvestTo, p.ClearOn, p.Notes, p.PlantCount(), Math.Round(p.UsedArea(), 3),
        PlantingPhases.Segments(p, from, to).Select(s => new PhaseSegmentDto(s.Phase, s.From, s.To)).ToList());

    public static PlantingWarningDto ToDto(this PlantingWarning w) => new(w.PlantingId, w.BedId, w.Level, w.Code, w.Date, w.Params);
}
