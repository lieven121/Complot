using Complot.Domain.Catalog;
using Complot.Domain.Common;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using Complot.Domain.Tools;

namespace Complot.Domain.Work;

/// <summary>Derives the work from the plan: planting steps, perennial and tree care, and manual tasks.</summary>
public static class TaskGenerator
{
    /// <summary>Base estimates per kind. Per-m² kinds scale with the planted area.</summary>
    public static readonly IReadOnlyDictionary<TaskKind, (int Minutes, bool PerSquareMeter)> Base = new Dictionary<TaskKind, (int, bool)>
    {
        [TaskKind.SowIndoor] = (20, false),
        [TaskKind.Sow] = (5, true),
        [TaskKind.PlantOut] = (8, true),
        [TaskKind.Plant] = (8, true),
        [TaskKind.Harvest] = (20, false),
        [TaskKind.Clear] = (6, true),
        [TaskKind.Care] = (30, false),
        [TaskKind.Custom] = (30, false),
        [TaskKind.Maintenance] = (30, false),
    };

    /// <summary>Plantings need Bed and Variety loaded.</summary>
    public static IEnumerable<GardenTask> ForPlanting(Planting p, int fromYear, int toYear)
    {
        var variety = p.Variety;
        var area = p.UsedArea();
        GardenTask Task(TaskKind kind, DateOnly date, int? year = null, string? title = null, int? minutes = null) =>
            new(title is null ? TaskKeys.Planting(p.Id, kind, year) : TaskKeys.PlantingCare(p.Id, title, date.Year),
                kind, date, p.Bed.SiteId, p.BedId, null, p.Id, variety.Name, title, minutes ?? Minutes(kind, area));

        if (variety.Life == Life.Perennial)
        {
            yield return Task(TaskKind.Plant, p.StartInBed);
            for (var year = fromYear; year <= toYear; year++)
            {
                var harvestStart = new DateOnly(year, variety.Harvest.From, 1);
                if (harvestStart > p.StartInBed && harvestStart < p.EndInBed) yield return Task(TaskKind.Harvest, harvestStart, year);
                foreach (var care in CareDates(variety.Actions, year, p.StartInBed, p.EndInBed))
                    yield return Task(TaskKind.Care, care.Date, title: care.Action.Title, minutes: care.Action.EstimatedMinutes);
            }
            if (p.ClearOn is { } removed) yield return Task(TaskKind.Clear, removed);
            yield break;
        }

        if (p.PlantOutOn is { } plantOut)
        {
            yield return Task(TaskKind.SowIndoor, p.SowOn);
            yield return Task(TaskKind.PlantOut, plantOut);
        }
        else yield return Task(TaskKind.Sow, p.SowOn);
        if (p.HarvestFrom is { } harvest) yield return Task(TaskKind.Harvest, harvest);
        if (p.ClearOn is { } clear) yield return Task(TaskKind.Clear, clear);
    }

    /// <summary>Needs the tree's Variety loaded (when it has one).</summary>
    public static IEnumerable<GardenTask> ForTree(Tree tree, int fromYear, int toYear)
    {
        for (var year = fromYear; year <= toYear; year++)
        foreach (var care in CareDates(tree.AllActions, year, tree.PlantedOn ?? DateOnly.MinValue, DateOnly.MaxValue))
            yield return new GardenTask(TaskKeys.TreeCare(tree.Id, care.Action.Title, year), TaskKind.Care, care.Date,
                tree.SiteId, null, tree.Id, null, tree.Name, care.Action.Title, care.Action.EstimatedMinutes);
    }

    /// <summary>
    /// Maintenance of a tool: the next due date (also when overdue) and the following ones up to <paramref name="to"/>, plus
    /// the maintenance done through tasks earlier (so checked-off tasks stay visible).
    /// </summary>
    public static IEnumerable<GardenTask> ForTool(Tool tool, DateOnly from, DateOnly to) => ToolTasks(tool, from, to).DistinctBy(t => t.Key);

    private static IEnumerable<GardenTask> ToolTasks(Tool tool, DateOnly from, DateOnly to)
    {
        GardenTask Task(string key, DateOnly due) =>
            new(key, TaskKind.Maintenance, due, null, null, null, null, tool.Name, null, tool.MaintenanceMinutes);

        foreach (var done in tool.Log.Where(e => e is { IsMaintenance: true, TaskKey: not null } && e.Date >= from && e.Date <= to))
            yield return Task(done.TaskKey!, done.Date);
        if (tool is not { NextMaintenance: { } next, MaintenanceIntervalMonths: { } months }) yield break;
        for (var due = next; due <= to; due = due.AddMonths(months))
            if (due == next || due >= from) yield return Task(TaskKeys.ToolMaintenance(tool.Id, due), due);
    }

    public static GardenTask ForManual(ManualTask task) =>
        new(task.SourceKey, TaskKind.Custom, task.DueOn, task.SiteId, task.BedId, null, null, null, task.Title, task.BaseMinutes);

    private static IEnumerable<(DateOnly Date, YearlyAction Action)> CareDates(IEnumerable<YearlyAction> actions, int year, DateOnly from, DateOnly until) =>
        actions.Select(a => (Date: new DateOnly(year, a.Month, 1), Action: a)).Where(x => x.Date >= from && x.Date < until);

    private static int Minutes(TaskKind kind, double area)
    {
        var (minutes, perSquareMeter) = Base[kind];
        return perSquareMeter ? Math.Max(5, (int)Math.Round(minutes * area)) : minutes;
    }
}

/// <summary>Learns how much real work takes compared to the base estimate, per kind of task.</summary>
public static class TaskEstimates
{
    /// <summary>Fewer completed samples than this and the base estimate is used unchanged.</summary>
    public const int MinimumSamples = 2;

    public sealed record Factor(TaskKind Kind, double Value, int Samples);

    public static IReadOnlyDictionary<TaskKind, Factor> Learn(IEnumerable<TaskState> states)
    {
        var samples = states
            .Where(s => s is { Done: true, ActualMinutes: > 0, BaseMinutesAtCompletion: > 0, KindAtCompletion: not null })
            .GroupBy(s => s.KindAtCompletion!.Value)
            .ToDictionary(g => g.Key, g => (Actual: g.Sum(s => s.ActualMinutes!.Value), Base: g.Sum(s => s.BaseMinutesAtCompletion!.Value), Count: g.Count()));

        return Enum.GetValues<TaskKind>().ToDictionary(kind => kind, kind =>
            samples.TryGetValue(kind, out var s) && s.Count >= MinimumSamples
                ? new Factor(kind, Math.Round((double)s.Actual / s.Base, 2), s.Count)
                : new Factor(kind, 1, samples.TryGetValue(kind, out var few) ? few.Count : 0));
    }

    /// <summary>Estimate in minutes, rounded to 5: a manual override wins, otherwise base × learned factor.</summary>
    public static int Estimate(GardenTask task, TaskState? state, IReadOnlyDictionary<TaskKind, Factor> factors) =>
        state?.EstimateOverride ?? RoundToFive(task.BaseMinutes * factors[task.Kind].Value);

    private static int RoundToFive(double minutes) => Math.Max(5, (int)(Math.Round(minutes / 5) * 5));
}
