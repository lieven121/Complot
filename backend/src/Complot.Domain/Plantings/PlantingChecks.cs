using Complot.Domain.Catalog;

namespace Complot.Domain.Plantings;

public enum WarningLevel
{
    Info,
    Warning,
}

/// <summary>A finding about a planting. <see cref="Code"/> + <see cref="Params"/> are translated by the UI.</summary>
public sealed record PlantingWarning(
    Guid PlantingId, Guid BedId, WarningLevel Level, string Code, DateOnly Date, IReadOnlyDictionary<string, object?> Params);

/// <summary>
/// Checks plantings against each other and against the beds: crop rotation per botanical family, bad neighbours
/// (same bed or neighbouring beds at the same time), sun, and how long perennials may stay.
/// Plantings must have Bed, Variety, Variety.Category, Variety.Family and Category.DefaultFamily loaded.
/// </summary>
public static class PlantingChecks
{
    public static List<PlantingWarning> Check(IReadOnlyList<Planting> plantings)
    {
        var warnings = new List<PlantingWarning>();
        foreach (var planting in plantings)
        {
            warnings.AddRange(Rotation(planting, plantings));
            warnings.AddRange(SunAndStay(planting));
        }
        warnings.AddRange(BadNeighbours(plantings));
        return warnings.OrderBy(w => w.Date).ToList();
    }

    /// <summary>Extra check for a planting being planned: is the sowing date inside the variety's window?</summary>
    public static PlantingWarning? OutsideSowingWindow(Planting planting)
    {
        var window = PlantingSchedule.RelevantWindow(planting.Variety, sownIndoors: planting.PlantOutOn is not null);
        return window is null || window.Contains(planting.SowOn) ? null
            : new PlantingWarning(planting.Id, planting.BedId, WarningLevel.Warning, "outsideWindow", planting.SowOn,
                new Dictionary<string, object?> { ["from"] = window.From, ["to"] = window.To });
    }

    private static IEnumerable<PlantingWarning> Rotation(Planting planting, IReadOnlyList<Planting> all)
    {
        var variety = planting.Variety;
        var rest = variety.RotationRestYears;
        var family = variety.EffectiveFamily;
        if (rest == 0 || family is null) yield break;

        var previous = all
            .Where(q => q != planting && q.BedId == planting.BedId && q.ClearOn is { } clear && clear <= planting.StartInBed
                        && q.Variety.RotationRestYears > 0 && q.Variety.EffectiveFamily?.Id == family.Id)
            .MaxBy(q => q.ClearOn);
        if (previous?.ClearOn is not { } until) yield break;

        var freeFrom = until.AddYears(rest);
        if (planting.StartInBed < freeFrom)
            yield return new PlantingWarning(planting.Id, planting.BedId, WarningLevel.Warning, "rotation", planting.StartInBed,
                new Dictionary<string, object?>
                {
                    ["family"] = family.LatinName, ["previous"] = previous.Variety.Name, ["until"] = until, ["rest"] = rest, ["freeFrom"] = freeFrom,
                });
    }

    private static IEnumerable<PlantingWarning> SunAndStay(Planting planting)
    {
        var variety = planting.Variety;
        if (!variety.Sun.Contains(planting.Bed.Sun))
            yield return new PlantingWarning(planting.Id, planting.BedId, WarningLevel.Info, "sun", planting.StartInBed,
                new Dictionary<string, object?> { ["variety"] = variety.Name, ["bedSun"] = planting.Bed.Sun });

        if (variety.Life != Life.Perennial || variety.MaxYearsInBed is not { } years) yield break;
        var limit = planting.StartInBed.AddYears(years);
        if (planting.ClearOn is null)
            yield return new PlantingWarning(planting.Id, planting.BedId, WarningLevel.Info, "renewBy", limit,
                new Dictionary<string, object?> { ["variety"] = variety.Name, ["date"] = limit, ["years"] = years });
        else if (planting.ClearOn > limit)
            yield return new PlantingWarning(planting.Id, planting.BedId, WarningLevel.Warning, "stayTooLong", limit,
                new Dictionary<string, object?> { ["variety"] = variety.Name, ["years"] = years, ["until"] = planting.ClearOn });
    }

    private static IEnumerable<PlantingWarning> BadNeighbours(IReadOnlyList<Planting> plantings)
    {
        for (var i = 0; i < plantings.Count; i++)
        for (var j = i + 1; j < plantings.Count; j++)
        {
            var (a, b) = (plantings[i], plantings[j]);
            var near = a.BedId == b.BedId || a.Bed.IsNeighbourOf(b.Bed);
            if (!near || !a.OverlapsInBed(b) || !AreBadNeighbours(a.Variety.Category, b.Variety.Category)) continue;

            var date = a.StartInBed > b.StartInBed ? a.StartInBed : b.StartInBed;
            var parameters = new Dictionary<string, object?>
            {
                ["a"] = a.Variety.Name, ["b"] = b.Variety.Name, ["bedA"] = a.Bed.Code, ["bedB"] = b.Bed.Code,
            };
            yield return new PlantingWarning(a.Id, a.BedId, WarningLevel.Warning, "badNeighbours", date, parameters);
            if (b.BedId != a.BedId) yield return new PlantingWarning(b.Id, b.BedId, WarningLevel.Warning, "badNeighbours", date, parameters);
        }
    }

    private static bool AreBadNeighbours(PlantCategory a, PlantCategory b) =>
        a.CompanionIds(CompanionRelation.Bad).Contains(b.Id) || b.CompanionIds(CompanionRelation.Bad).Contains(a.Id);
}
