using Complot.Domain.Catalog;
using Complot.Domain.Common;
using Complot.Domain.Garden;
using Complot.Domain.Work;
using static Complot.Domain.Tests.Plantings.PlantingTestData;

namespace Complot.Domain.Tests.Work;

public sealed class TaskGeneratorTests
{
    [Fact]
    public void Pre_sown_annual_gives_sow_indoor_plant_out_harvest_and_clear()
    {
        var planting = Plant(Bed("K1", 0), CherryTomato, new DateOnly(2026, 2, 20));
        var tasks = TaskGenerator.ForPlanting(planting, 2026, 2026).ToList();

        Assert.Equal([TaskKind.SowIndoor, TaskKind.PlantOut, TaskKind.Harvest, TaskKind.Clear], tasks.Select(t => t.Kind));
        Assert.Equal(new DateOnly(2026, 4, 17), tasks[1].Date);
        Assert.Equal(38, tasks[1].BaseMinutes); // 8 min/m² × 4.8 m²
        Assert.Equal(tasks.Count, tasks.Select(t => t.Key).Distinct().Count());
    }

    [Fact]
    public void Spot_task_minutes_scale_with_its_plants_not_the_bed()
    {
        var spot = Spot(SpotsBed("M1"), CherryTomato, new DateOnly(2026, 2, 20), 2);
        var plantOut = TaskGenerator.ForPlanting(spot, 2026, 2026).Single(t => t.Kind == TaskKind.PlantOut);
        Assert.Equal(6, plantOut.BaseMinutes); // 8 min/m² × 0.7 m²
    }

    [Fact]
    public void Perennial_gets_a_harvest_and_its_care_actions_every_year()
    {
        Strawberry.Actions = [new YearlyAction { Title = "Uitlopers verwijderen", Month = 7, EstimatedMinutes = 40 }];
        var planting = Plant(Bed("F3", 0), Strawberry, new DateOnly(2026, 8, 20));
        var tasks = TaskGenerator.ForPlanting(planting, 2026, 2028).ToList();

        Assert.Equal(1, tasks.Count(t => t.Kind == TaskKind.Plant));
        Assert.Equal(2, tasks.Count(t => t.Kind == TaskKind.Harvest)); // 2027, 2028 (2026 is before planting)
        var care = tasks.Where(t => t.Kind == TaskKind.Care).ToList();
        Assert.Equal(2, care.Count);
        Assert.All(care, t => Assert.Equal(40, t.BaseMinutes));
    }

    [Fact]
    public void Tree_care_starts_after_planting()
    {
        var tree = new Tree
        {
            Name = "Appelboom", PlantedOn = new DateOnly(2025, 3, 1),
            Actions = [new YearlyAction { Title = "Wintersnoei", Month = 2, EstimatedMinutes = 90 }],
        };
        var tasks = TaskGenerator.ForTree(tree, 2025, 2027).ToList();
        Assert.Equal([2026, 2027], tasks.Select(t => t.Date.Year));
        Assert.Equal(tasks[0].Key, TaskKeys.TreeCare(tree.Id, " wintersnoei ", 2026));
    }

    [Fact]
    public void A_tree_of_a_variety_follows_its_care_and_its_own_action_replaces_one_of_the_same_title()
    {
        var variety = new PlantVariety
        {
            Name = "Appel", Category = new PlantCategory { Slug = "fruit", Name = "Fruitbomen" }, Harvest = new MonthWindow(9, 10),
            Actions = [new YearlyAction { Title = "Wintersnoei", Month = 2, EstimatedMinutes = 90 }, new YearlyAction { Title = "Dunnen", Month = 6, EstimatedMinutes = 30 }],
        };
        var tree = new Tree
        {
            Name = "Leiappel", Variety = variety,
            Actions = [new YearlyAction { Title = "wintersnoei", Month = 3, EstimatedMinutes = 45 }, new YearlyAction { Title = "Zomersnoei", Month = 8, EstimatedMinutes = 40 }],
        };
        var tasks = TaskGenerator.ForTree(tree, 2027, 2027).OrderBy(t => t.Date).ToList();
        Assert.Equal([("wintersnoei", 3), ("Dunnen", 6), ("Zomersnoei", 8)], tasks.Select(t => (t.Title!, t.Date.Month)));
    }

    [Fact]
    public void Task_keys_can_be_parsed_back_to_their_source()
    {
        var id = Guid.NewGuid();
        Assert.Equal(("planting", id), TaskKeys.Parse(TaskKeys.Planting(id, TaskKind.Harvest, 2026)));
        Assert.Null(TaskKeys.Parse("nonsense"));
    }
}

public sealed class TaskEstimatesTests
{
    private static TaskState Done(TaskKind kind, int baseMinutes, int actual) => new()
    {
        SourceKey = Guid.NewGuid().ToString(), Done = true, KindAtCompletion = kind, BaseMinutesAtCompletion = baseMinutes, ActualMinutes = actual,
    };

    private static GardenTask Task(TaskKind kind, int baseMinutes) => new("k", kind, default, null, null, null, null, null, null, baseMinutes);

    [Fact]
    public void Factor_is_total_actual_over_total_base_once_there_are_enough_samples()
    {
        var factors = TaskEstimates.Learn([Done(TaskKind.Clear, 30, 40), Done(TaskKind.Clear, 30, 41), Done(TaskKind.Sow, 20, 10)]);
        Assert.Equal(1.35, factors[TaskKind.Clear].Value);
        Assert.Equal(2, factors[TaskKind.Clear].Samples);
        Assert.Equal(1, factors[TaskKind.Sow].Value); // only one sample
        Assert.Equal(1, factors[TaskKind.Sow].Samples);
    }

    [Fact]
    public void Estimate_applies_the_factor_and_rounds_to_five_minutes()
    {
        var factors = TaskEstimates.Learn([Done(TaskKind.Clear, 30, 40), Done(TaskKind.Clear, 30, 41)]);
        Assert.Equal(40, TaskEstimates.Estimate(Task(TaskKind.Clear, 29), null, factors)); // 29 × 1.35 = 39.2
        Assert.Equal(15, TaskEstimates.Estimate(Task(TaskKind.Clear, 29), new TaskState { SourceKey = "k", EstimateOverride = 15 }, factors));
    }
}
