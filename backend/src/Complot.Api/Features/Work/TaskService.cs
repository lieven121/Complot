using Complot.Api.Data;
using Complot.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Work;

/// <summary>Generates the tasks of the plan and finds a single task by its key (to update its state).</summary>
public sealed class TaskService(ComplotDbContext db)
{
    /// <summary>Years searched when a single task is looked up by key (perennials and trees repeat yearly).</summary>
    private const int FirstYear = 2000;
    private const int LastYear = 2100;

    public async Task<List<GardenTask>> GenerateAsync(DateOnly from, DateOnly to, Guid? siteId, CancellationToken ct)
    {
        var plantings = await db.Plantings.AsNoTracking().Include(p => p.Bed).Include(p => p.Variety)
            .Where(p => (siteId == null || p.Bed.SiteId == siteId) && p.SowOn <= to && (p.ClearOn == null || p.ClearOn >= from))
            .ToListAsync(ct);
        var trees = await db.Trees.AsNoTracking().Where(t => siteId == null || t.SiteId == siteId).ToListAsync(ct);
        var manual = await db.ManualTasks.AsNoTracking()
            .Where(t => (siteId == null || t.SiteId == siteId) && t.DueOn >= from && t.DueOn <= to)
            .ToListAsync(ct);

        return plantings.SelectMany(p => TaskGenerator.ForPlanting(p, from.Year, to.Year))
            .Concat(trees.SelectMany(t => TaskGenerator.ForTree(t, from.Year, to.Year)))
            .Concat(manual.Select(TaskGenerator.ForManual))
            .Where(t => t.Date >= from && t.Date <= to)
            .OrderBy(t => t.Date)
            .ToList();
    }

    public async Task<GardenTask?> FindAsync(string key, CancellationToken ct)
    {
        if (TaskKeys.Parse(key) is not var (source, id)) return null;
        var candidates = source switch
        {
            "planting" => await db.Plantings.AsNoTracking().Include(p => p.Bed).Include(p => p.Variety).FirstOrDefaultAsync(p => p.Id == id, ct)
                is { } planting ? TaskGenerator.ForPlanting(planting, FirstYear, LastYear) : [],
            "tree" => await db.Trees.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
                is { } tree ? TaskGenerator.ForTree(tree, FirstYear, LastYear) : [],
            "manual" => await db.ManualTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
                is { } manual ? [TaskGenerator.ForManual(manual)] : [],
            _ => [],
        };
        return candidates.FirstOrDefault(t => t.Key == key);
    }

    public async Task<IReadOnlyDictionary<TaskKind, TaskEstimates.Factor>> FactorsAsync(CancellationToken ct) =>
        TaskEstimates.Learn(await db.TaskStates.AsNoTracking().Where(s => s.Done && s.ActualMinutes != null).ToListAsync(ct));
}
