using Complot.Domain.Catalog;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;

namespace Complot.Domain.Tests.Plantings;

/// <summary>Small in-memory garden for the planting rule tests.</summary>
internal static class PlantingTestData
{
    public static readonly PlantFamily Solanaceae = new() { LatinName = "Solanaceae", NameNl = "nachtschade", NameEn = "nightshades", RotationRestYears = 4 };
    public static readonly PlantFamily Fabaceae = new() { LatinName = "Fabaceae", NameNl = "vlinderbloemigen", NameEn = "legumes", RotationRestYears = 3 };
    public static readonly PlantFamily Amaryllidaceae = new() { LatinName = "Amaryllidaceae", NameNl = "look", NameEn = "alliums", RotationRestYears = 3 };
    public static readonly PlantFamily Rosaceae = new() { LatinName = "Rosaceae", NameNl = "rozen", NameEn = "roses", RotationRestYears = 3 };

    public static readonly PlantCategory Tomatoes = new() { Slug = "tomaat", Name = "Tomaten", DefaultFamily = Solanaceae };
    public static readonly PlantCategory Potatoes = new() { Slug = "aardappel", Name = "Aardappelen", DefaultFamily = Solanaceae };
    public static readonly PlantCategory Onions = new() { Slug = "ui", Name = "Uien", DefaultFamily = Amaryllidaceae };
    public static readonly PlantCategory Beans = new() { Slug = "boon", Name = "Bonen", DefaultFamily = Fabaceae };
    public static readonly PlantCategory Strawberries = new() { Slug = "aardbei", Name = "Aardbeien", DefaultFamily = Rosaceae };

    static PlantingTestData()
    {
        Beans.Companions.Add(new CategoryCompanion { CategoryId = Beans.Id, OtherCategoryId = Onions.Id, Relation = CompanionRelation.Bad });
    }

    public static readonly PlantVariety CherryTomato = new()
    {
        Name = "Kerstomaat", Category = Tomatoes, Sun = [SunExposure.Full], SowIndoor = new MonthWindow(2, 4), WeeksIndoor = 8,
        PlantOut = new MonthWindow(5, 6), DaysToFirstHarvest = 120, HarvestWeeks = 12, Harvest = new MonthWindow(7, 10),
        SpacingInRowCm = 50, SpacingBetweenRowsCm = 70,
    };

    public static readonly PlantVariety Potato = new()
    {
        Name = "Aardappel", Category = Potatoes, Sun = [SunExposure.Full], SowOutdoor = new MonthWindow(4, 5),
        DaysToFirstHarvest = 100, HarvestWeeks = 6, Harvest = new MonthWindow(7, 9),
    };

    public static readonly PlantVariety Onion = new()
    {
        Name = "Ui", Category = Onions, Sun = [SunExposure.Full], SowOutdoor = new MonthWindow(3, 4),
        DaysToFirstHarvest = 120, HarvestWeeks = 4, Harvest = new MonthWindow(7, 8),
    };

    public static readonly PlantVariety Bean = new()
    {
        Name = "Stamboon", Category = Beans, Sun = [SunExposure.Full], SowOutdoor = new MonthWindow(5, 7),
        DaysToFirstHarvest = 60, HarvestWeeks = 4, Harvest = new MonthWindow(7, 9),
    };

    public static readonly PlantVariety Strawberry = new()
    {
        Name = "Aardbei", Category = Strawberries, Life = Life.Perennial, Sun = [SunExposure.Full], PlantOut = new MonthWindow(7, 9),
        Harvest = new MonthWindow(6, 7), MaxYearsInBed = 2,
    };

    public static Bed Bed(string code, double x, Guid? plotId = null, SunExposure sun = SunExposure.Full) =>
        new() { Code = code, PlotId = plotId ?? PlotId, Shape = new Shape(ShapeKind.Rect, x, 0, 1.2, 4), Sun = sun };

    public static Bed SpotsBed(string code, double x = 0) =>
        new() { Code = code, PlotId = PlotId, Kind = BedKind.Spots, Shape = new Shape(ShapeKind.Rect, x, 0, 1, 1) };

    public static readonly Guid PlotId = Guid.NewGuid();

    /// <summary>A spot of <paramref name="count"/> plants in the middle of a spots bed.</summary>
    public static Planting Spot(Bed bed, PlantVariety variety, DateOnly sowOn, int count)
    {
        var planting = Plant(bed, variety, sowOn);
        (planting.Count, planting.SpotX, planting.SpotY) = (count, 0.5, 0.5);
        return planting;
    }

    public static Planting Plant(Bed bed, PlantVariety variety, DateOnly sowOn, bool direct = false)
    {
        var planting = new Planting { Bed = bed, BedId = bed.Id, Variety = variety, VarietyId = variety.Id };
        planting.ApplyDates(PlantingSchedule.Calculate(variety, sowOn, direct));
        return planting;
    }
}
