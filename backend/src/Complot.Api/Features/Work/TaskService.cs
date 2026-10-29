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

    /// <summary>Tasks of <paramref name="siteIds"/>; <paramref name="includeSiteless"/> adds manual tasks that belong to no site.</summary>
    public async Task<List<GardenTask>> GenerateAsync(
        DateOnly from, DateOnly to, IReadOnlyCollection<Guid> siteIds, bool includeSiteless, CancellationToken ct)
    {
        var plantings = await db.Plantings.AsNoTracking().Include(p => p.Bed).Include(p => p.Variety)
            .Where(p => siteIds.Contains(p.Bed.SiteId) && p.SowOn <= to && (p.ClearOn == null || p.ClearOn >= from))
            .ToListAsync(ct);
        var trees = await db.Trees.AsNoTracking().Include(t => t.Variety).Where(t => siteIds.Contains(t.SiteId)).ToListAsync(ct);
        var manual = await db.ManualTasks.AsNoTracking()
            .Where(t => (t.SiteId == null ? includeSiteless : siteIds.Contains(t.SiteId.Value)) && t.DueOn >= from && t.DueOn <= to)
            .ToListAsync(ct);

        // Tools belong to no site: their maintenance shows wherever site-less tasks do.
        var tools = includeSiteless ? await db.Tools.AsNoTracking().ToListAsync(ct) : [];

        return plantings.SelectMany(p => TaskGenerator.ForPlanting(p, from.Year, to.Year))
            .Concat(trees.SelectMany(t => TaskGenerator.ForTree(t, from.Year, to.Year)))
            .Concat(manual.Select(TaskGenerator.ForManual))
            .Concat(tools.SelectMany(t => TaskGenerator.ForTool(t, from, to)))
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
            "tree" => await db.Trees.AsNoTracking().Include(t => t.Variety).FirstOrDefaultAsync(t => t.Id == id, ct)
                is { } tree ? TaskGenerator.ForTree(tree, FirstYear, LastYear) : [],
            "manual" => await db.ManualTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
                is { } manual ? [TaskGenerator.ForManual(manual)] : [],
            "tool" => await db.Tools.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
                is { } tool ? TaskGenerator.ForTool(tool, new DateOnly(FirstYear, 1, 1), new DateOnly(LastYear, 12, 31)) : [],
            _ => [],
        };
        return candidates.FirstOrDefault(t => t.Key == key);
    }

    public async Task<IReadOnlyDictionary<TaskKind, TaskEstimates.Factor>> FactorsAsync(CancellationToken ct) =>
        TaskEstimates.Learn(await db.TaskStates.AsNoTracking().Where(s => s.Done && s.ActualMinutes != null).ToListAsync(ct));
}
