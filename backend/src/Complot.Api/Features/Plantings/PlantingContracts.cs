using Complot.Domain.Plantings;

namespace Complot.Api.Features.Plantings;

public sealed record PhaseSegmentDto(PlantingPhase Phase, DateOnly From, DateOnly To);

/// <summary>A planting with its phase segments for the requested period (the map just looks up the segment of a date).</summary>
public sealed record PlantingDto(
    Guid Id, Guid BedId, Guid VarietyId, double Share,
    DateOnly SowOn, DateOnly? PlantOutOn, DateOnly? HarvestFrom, DateOnly? HarvestTo, DateOnly? ClearOn,
    string? Notes, int? PlantCount, IReadOnlyList<PhaseSegmentDto> Phases);

/// <summary>Rule finding; the UI translates <see cref="Code"/> with <see cref="Params"/>.</summary>
public sealed record PlantingWarningDto(
    Guid PlantingId, Guid BedId, WarningLevel Level, string Code, DateOnly Date, IReadOnlyDictionary<string, object?> Params);

/// <summary>Plan a new planting: the dates follow from the variety and the sowing date.</summary>
public sealed record PlanPlantingInput(Guid BedId, Guid VarietyId, double Share, DateOnly SowOn, bool Direct, string? Notes);

public sealed record PlantingPreviewDto(PlantingDto Planting, IReadOnlyList<PlantingWarningDto> Warnings);

/// <summary>Adjust a planting by hand (e.g. the actual planting-out or harvest dates).</summary>
public sealed record UpdatePlantingInput(
    double Share, DateOnly SowOn, DateOnly? PlantOutOn, DateOnly? HarvestFrom, DateOnly? HarvestTo, DateOnly? ClearOn, string? Notes);

public static class PlantingMapping
{
    public static PlantingDto ToDto(this Planting p, DateOnly from, DateOnly to) => new(
        p.Id, p.BedId, p.VarietyId, p.Share, p.SowOn, p.PlantOutOn, p.HarvestFrom, p.HarvestTo, p.ClearOn, p.Notes, p.PlantCount(),
        PlantingPhases.Segments(p, from, to).Select(s => new PhaseSegmentDto(s.Phase, s.From, s.To)).ToList());

    public static PlantingWarningDto ToDto(this PlantingWarning w) => new(w.PlantingId, w.BedId, w.Level, w.Code, w.Date, w.Params);
}
