using Complot.Domain.Catalog;

namespace Complot.Domain.Plantings;

public enum PlantingPhase
{
    /// <summary>Sown indoors, not in the bed yet.</summary>
    Indoor,
    /// <summary>First weeks after sowing or planting out.</summary>
    Young,
    Growing,
    Harvest,
    /// <summary>After the harvest, until the bed is cleared.</summary>
    Clearing,
    /// <summary>Perennial in winter rest.</summary>
    Dormant,
}

/// <summary>A phase from <see cref="From"/> to <see cref="To"/>, both inclusive.</summary>
public sealed record PhaseSegment(PlantingPhase Phase, DateOnly From, DateOnly To);

/// <summary>
/// The phases of a planting over time. The UI only looks up the segment that contains a date, so the phase rules
/// live here and nowhere else.
/// </summary>
public static class PlantingPhases
{
    public const int YoungDays = 21;

    public static IReadOnlyList<PhaseSegment> Segments(Planting planting, DateOnly rangeFrom, DateOnly rangeTo)
    {
        var segments = planting.Variety.Life == Life.Perennial ? Perennial(planting, rangeTo) : Annual(planting);
        return segments
            .Where(s => s.From <= s.To && s.To >= rangeFrom && s.From <= rangeTo)
            .Select(s => s with { From = Max(s.From, rangeFrom), To = Min(s.To, rangeTo) })
            .ToList();
    }

    private static IEnumerable<PhaseSegment> Annual(Planting p)
    {
        var start = p.StartInBed;
        var harvestFrom = p.HarvestFrom ?? p.ClearOn ?? start.AddDays(90);
        var harvestTo = p.HarvestTo ?? harvestFrom;
        var clearOn = p.ClearOn ?? harvestTo;

        if (p.PlantOutOn is { } plantOut) yield return new(PlantingPhase.Indoor, p.SowOn, plantOut.AddDays(-1));
        var youngEnd = Min(start.AddDays(YoungDays - 1), harvestFrom.AddDays(-1));
        yield return new(PlantingPhase.Young, start, youngEnd);
        yield return new(PlantingPhase.Growing, youngEnd.AddDays(1), harvestFrom.AddDays(-1));
        yield return new(PlantingPhase.Harvest, harvestFrom, harvestTo);
        yield return new(PlantingPhase.Clearing, harvestTo.AddDays(1), clearOn);
    }

    /// <summary>Month by month: harvest months, winter rest (Dec–Feb) and growing otherwise; consecutive months merged.</summary>
    private static IEnumerable<PhaseSegment> Perennial(Planting p, DateOnly rangeTo)
    {
        var end = Min(p.ClearOn ?? rangeTo, rangeTo);
        var harvest = p.Variety.Harvest;
        PhaseSegment? current = null;
        for (var day = p.StartInBed; day <= end; day = new DateOnly(day.Year, day.Month, 1).AddMonths(1))
        {
            var phase = harvest.Contains(day.Month) ? PlantingPhase.Harvest
                : day.Month is 12 or 1 or 2 ? PlantingPhase.Dormant
                : PlantingPhase.Growing;
            var monthEnd = Min(new DateOnly(day.Year, day.Month, 1).AddMonths(1).AddDays(-1), end);
            if (current is not null && current.Phase == phase) current = current with { To = monthEnd };
            else
            {
                if (current is not null) yield return current;
                current = new PhaseSegment(phase, day, monthEnd);
            }
        }
        if (current is not null) yield return current;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
