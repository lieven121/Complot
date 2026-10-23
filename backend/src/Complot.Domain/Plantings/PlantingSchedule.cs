using Complot.Domain.Catalog;

namespace Complot.Domain.Plantings;

/// <summary>Calculates planting dates from the variety's metadata.</summary>
public static class PlantingSchedule
{
    public const int DefaultWeeksIndoor = 6;

    /// <summary>Days between the end of the harvest and the bed being free again.</summary>
    public const int ClearingDays = 7;

    /// <summary>
    /// Indoor sowing (unless <paramref name="direct"/>) → plant out after the weeks indoors → first harvest after
    /// <see cref="PlantVariety.DaysToFirstHarvest"/> from sowing → harvest weeks → a week to clear.
    /// Perennials stay until removed: no harvest/clear dates.
    /// </summary>
    public static PlantingDates Calculate(PlantVariety variety, DateOnly sowOn, bool direct = false)
    {
        DateOnly? plantOut = variety.SowIndoor is not null && !direct
            ? sowOn.AddDays(7 * (variety.WeeksIndoor ?? DefaultWeeksIndoor))
            : null;
        if (variety.Life == Life.Perennial) return new PlantingDates(sowOn, plantOut, null, null, null);

        var start = plantOut ?? sowOn;
        var harvestFrom = sowOn.AddDays(variety.DaysToFirstHarvest ?? 90);
        if (harvestFrom <= start) harvestFrom = start.AddDays(14);
        var harvestTo = harvestFrom.AddDays(7 * (variety.HarvestWeeks ?? 4));
        return new PlantingDates(sowOn, plantOut, harvestFrom, harvestTo, harvestTo.AddDays(ClearingDays));
    }

    /// <summary>The sowing window that applies: indoor when pre-sown, otherwise outdoor sowing or planting out.</summary>
    public static MonthWindow? RelevantWindow(PlantVariety variety, bool sownIndoors) =>
        sownIndoors ? variety.SowIndoor : variety.SowOutdoor ?? variety.PlantOut;
}
