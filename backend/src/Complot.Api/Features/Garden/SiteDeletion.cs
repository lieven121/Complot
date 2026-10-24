using Complot.Api.Data;
using Complot.Api.Hosting;
using Complot.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Complot.Api.Features.Garden;

/// <summary>
/// Removes a site with everything on it: plantings, beds and the rest of the layout, notes (and their photos), manual
/// tasks and memberships. Completed task states stay: they are the history the time estimates learn from.
/// </summary>
public sealed class SiteDeletion(ComplotDbContext db, IOptions<StorageOptions> storage)
{
    public async Task DeleteAsync(Guid siteId, CancellationToken ct)
    {
        var plantingIds = await db.Plantings.Where(p => p.Bed.SiteId == siteId).Select(p => p.Id).ToListAsync(ct);
        var treeIds = await db.Trees.Where(t => t.SiteId == siteId).Select(t => t.Id).ToListAsync(ct);
        var manualIds = await db.ManualTasks.Where(t => t.SiteId == siteId).Select(t => t.Id).ToListAsync(ct);
        var photos = await db.Notes.Where(n => n.SiteId == siteId && n.PhotoFileName != null).Select(n => n.PhotoFileName!).ToListAsync(ct);

        var sources = plantingIds.Select(id => ("planting", id))
            .Concat(treeIds.Select(id => ("tree", id)))
            .Concat(manualIds.Select(id => ("manual", id)))
            .ToHashSet();
        var openStates = (await db.TaskStates.Where(s => !s.Done).ToListAsync(ct))
            .Where(s => TaskKeys.Parse(s.SourceKey) is { } source && sources.Contains(source))
            .ToList();

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            db.TaskStates.RemoveRange(openStates);
            await db.SaveChangesAsync(ct);
            // Plantings and beds restrict deletion of what they point to, so they go first; the rest cascades from the site.
            await db.Plantings.Where(p => plantingIds.Contains(p.Id)).ExecuteDeleteAsync(ct);
            await db.Beds.Where(b => b.SiteId == siteId).ExecuteDeleteAsync(ct);
            await db.Sites.Where(s => s.Id == siteId).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        }

        foreach (var file in photos) File.Delete(Path.Combine(storage.Value.PhotoDir, file));
    }
}
