using Complot.Domain.Catalog;
using Complot.Domain.Tools;
using Complot.Domain.Work;
using static Complot.Domain.Tests.Plantings.PlantingTestData;

namespace Complot.Domain.Tests.Tools;

public sealed class ToolMaintenanceTests
{
    private static Tool Hoe() => new()
    {
        Name = "Trekschoffel", Kind = "Handgereedschap", QtyGood = 2, BoughtOn = new DateOnly(2026, 1, 10),
        MaintenanceIntervalMonths = 6, MaintenanceMinutes = 20, LifespanYears = 15,
    };

    [Fact]
    public void Next_maintenance_and_replacement_follow_from_purchase_and_log()
    {
        var tool = Hoe();
        Assert.Equal(new DateOnly(2026, 7, 10), tool.NextMaintenance);
        Assert.Equal(new DateOnly(2041, 1, 10), tool.ReplaceBy);
        tool.Log.Add(new ToolLogEntry { Date = new DateOnly(2026, 8, 1), IsMaintenance = true });
        tool.Log.Add(new ToolLogEntry { Date = new DateOnly(2026, 9, 1), Text = "Steel gebroken" });
        Assert.Equal(new DateOnly(2027, 2, 1), tool.NextMaintenance);
    }

    [Fact]
    public void Tasks_show_the_overdue_one_and_the_ones_ahead()
    {
        var tasks = TaskGenerator.ForTool(Hoe(), new DateOnly(2026, 9, 1), new DateOnly(2027, 12, 31)).ToList();
        Assert.Equal([new DateOnly(2026, 7, 10), new DateOnly(2027, 1, 10), new DateOnly(2027, 7, 10)], tasks.Select(t => t.Date));
        Assert.All(tasks, t => Assert.Equal((TaskKind.Maintenance, 20, "Trekschoffel"), (t.Kind, t.BaseMinutes, t.Subject)));
        Assert.Null(tasks[0].SiteId);
    }

    [Fact]
    public void Maintenance_done_through_a_task_stays_listed_and_moves_the_next_one()
    {
        var tool = Hoe();
        var key = TaskKeys.ToolMaintenance(tool.Id, new DateOnly(2026, 7, 10));
        tool.Log.Add(new ToolLogEntry { Date = new DateOnly(2026, 8, 1), IsMaintenance = true, TaskKey = key });
        var tasks = TaskGenerator.ForTool(tool, new DateOnly(2026, 7, 1), new DateOnly(2027, 3, 1)).ToList();
        Assert.Equal([(key, new DateOnly(2026, 8, 1)), (TaskKeys.ToolMaintenance(tool.Id, new DateOnly(2027, 2, 1)), new DateOnly(2027, 2, 1))],
            tasks.Select(t => (t.Key, t.Date)));
    }

    [Fact]
    public void No_interval_no_tasks() =>
        Assert.Empty(TaskGenerator.ForTool(new Tool { Name = "Gaas", Kind = "Bescherming" }, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1)));
}

public sealed class ToolDemandTests
{
    private static readonly Guid Spiral = Guid.NewGuid();
    private static readonly Guid Net = Guid.NewGuid();

    /// <summary>A copy of a test variety with tool needs (the shared ones stay untouched).</summary>
    private static PlantVariety With(PlantVariety v, params ToolNeed[] needs) => new()
    {
        Name = v.Name, Category = v.Category, Sun = v.Sun, SowIndoor = v.SowIndoor, WeeksIndoor = v.WeeksIndoor, SowOutdoor = v.SowOutdoor,
        PlantOut = v.PlantOut, DaysToFirstHarvest = v.DaysToFirstHarvest, HarvestWeeks = v.HarvestWeeks, Harvest = v.Harvest,
        SpacingInRowCm = v.SpacingInRowCm, SpacingBetweenRowsCm = v.SpacingBetweenRowsCm, ToolNeeds = [.. needs],
    };

    [Fact]
    public void Peak_counts_per_plant_and_per_bed_needs_of_crops_in_beds_at_the_same_time()
    {
        var tomato = With(CherryTomato, new ToolNeed { ToolId = Spiral, Per = NeedPer.Plant, Quantity = 1 });
        var potato = With(Potato, new ToolNeed { ToolId = Net, Per = NeedPer.Bed, Quantity = 1 });
        var plantings = new[]
        {
            Plant(Bed("K1", 0), tomato, new DateOnly(2026, 2, 20)),   // 4.8 m² at 50 × 70 cm → 13 plants
            Plant(Bed("K2", 2), tomato, new DateOnly(2026, 2, 20)),
            Plant(Bed("A1", 4), potato, new DateOnly(2026, 4, 10), direct: true),
        };

        var peaks = ToolDemand.PeaksIn(plantings, 2026).ToDictionary(p => p.ToolId);
        Assert.Equal((26, 5), (peaks[Spiral].Quantity, peaks[Spiral].Month));
        Assert.Equal(1, peaks[Net].Quantity);
        Assert.Empty(ToolDemand.PeaksIn(plantings, 2030));
    }
}
