using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Garden;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Garden;

public static class SiteEndpoints
{
    public static void MapSites(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/sites").WithTags("Sites");
        group.MapGet("/", List).WithName("ListSites");
        group.MapPost("/", Create).WithName("CreateSite").Validate<SiteInput>();
        group.MapPut("/{id:guid}", Update).WithName("UpdateSite").Validate<SiteInput>();
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteSite");
        group.MapGet("/{id:guid}/layout", GetLayout).WithName("GetSiteLayout");
        group.MapPut("/{id:guid}/layout", SaveLayout).WithName("SaveSiteLayout").Validate<SiteLayoutDto>();
    }

    private static async Task<SiteDto[]> List(ComplotDbContext db, CancellationToken ct)
    {
        var bedCounts = await db.Beds.GroupBy(b => b.SiteId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return (await db.Sites.AsNoTracking().OrderBy(s => s.CreatedAt).ToListAsync(ct))
            .Select(s => new SiteDto(s.Id, s.Name, s.Notes, bedCounts.GetValueOrDefault(s.Id))).ToArray();
    }

    private static async Task<Created<SiteDto>> Create(SiteInput input, ComplotDbContext db, CancellationToken ct)
    {
        var site = new Site { Name = input.Name.Trim(), Notes = input.Notes };
        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/sites/{site.Id}", new SiteDto(site.Id, site.Name, site.Notes, 0));
    }

    private static async Task<Results<Ok<SiteDto>, ProblemHttpResult>> Update(Guid id, SiteInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Sites.FindAsync([id], ct) is not { } site) return ApiProblems.NotFound();
        site.Name = input.Name.Trim();
        site.Notes = input.Notes;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new SiteDto(site.Id, site.Name, site.Notes, await db.Beds.CountAsync(b => b.SiteId == id, ct)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Sites.FindAsync([id], ct) is not { } site) return ApiProblems.NotFound();
        if (await db.Beds.AnyAsync(b => b.SiteId == id, ct)) return ApiProblems.Conflict("site.hasBeds");
        db.Sites.Remove(site);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<SiteLayoutDto>, ProblemHttpResult>> GetLayout(Guid id, ComplotDbContext db, CancellationToken ct) =>
        await db.Sites.AnyAsync(s => s.Id == id, ct) ? TypedResults.Ok(await LoadLayout(id, db, ct)) : ApiProblems.NotFound();

    private static async Task<Results<Ok<SiteLayoutDto>, ProblemHttpResult>> SaveLayout(
        Guid id, SiteLayoutDto layout, ComplotDbContext db, CancellationToken ct)
    {
        if (!await db.Sites.AnyAsync(s => s.Id == id, ct)) return ApiProblems.NotFound();

        var plots = await db.Plots.Where(x => x.SiteId == id).ToListAsync(ct);
        var beds = await db.Beds.Where(x => x.SiteId == id).ToListAsync(ct);
        var paths = await db.Paths.Where(x => x.SiteId == id).ToListAsync(ct);
        var features = await db.Features.Where(x => x.SiteId == id).ToListAsync(ct);
        var trees = await db.Trees.Where(x => x.SiteId == id).ToListAsync(ct);

        if (await UsesIdsOfOtherSitesAsync(id, layout, db, ct)) return ApiProblems.Conflict("layout.foreignId");
        var removedBedIds = beds.Select(b => b.Id).Except(layout.Beds.Select(b => b.Id)).ToList();
        if (await BedUsage.AnyInUseAsync(db, removedBedIds, ct)) return ApiProblems.Conflict("bed.inUse");

        db.Plots.SyncById(plots, layout.Plots, p => p.Id,
            p => new Plot { Id = p.Id, SiteId = id, Name = "", Shape = p.Shape.ToDomain() },
            (p, e) => { e.Name = p.Name.Trim(); e.Type = p.Type; e.Shape = p.Shape.ToDomain(); });
        db.Beds.SyncById(beds, layout.Beds, b => b.Id,
            b => new Bed { Id = b.Id, SiteId = id, Code = "", Shape = b.Shape.ToDomain() },
            (b, e) => { e.PlotId = b.PlotId; e.Code = b.Code.Trim(); e.Shape = b.Shape.ToDomain(); e.Sun = b.Sun; e.Soil = Clean(b.Soil); e.Notes = Clean(b.Notes); });
        db.Paths.SyncById(paths, layout.Paths, p => p.Id,
            p => new GardenPath { Id = p.Id, SiteId = id, Name = "", Shape = p.Shape.ToDomain() },
            (p, e) => { e.Name = p.Name.Trim(); e.Material = p.Material; e.Shape = p.Shape.ToDomain(); });
        db.Features.SyncById(features, layout.Features, f => f.Id,
            f => new GardenFeature { Id = f.Id, SiteId = id, Name = "", Shape = f.Shape.ToDomain() },
            (f, e) => { e.Name = f.Name.Trim(); e.Type = f.Type; e.Shape = f.Shape.ToDomain(); });
        db.Trees.SyncById(trees, layout.Trees, t => t.Id,
            t => new Tree { Id = t.Id, SiteId = id, Name = "" },
            (t, e) =>
            {
                e.Name = t.Name.Trim();
                e.Cultivar = Clean(t.Cultivar);
                e.Kind = t.Kind;
                e.X = t.X;
                e.Y = t.Y;
                e.CrownDiameter = t.CrownDiameter;
                e.PlantedOn = t.PlantedOn;
                e.Actions = t.Actions.ToDomain();
            });

        if (await db.SaveOrConflictAsync("bed.duplicateCode", ct) is { } conflict) return conflict;
        return TypedResults.Ok(await LoadLayout(id, db, ct));
    }

    public static async Task<SiteLayoutDto> LoadLayout(Guid siteId, ComplotDbContext db, CancellationToken ct) => new(
        (await db.Plots.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        (await db.Beds.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Code).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        (await db.Paths.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        (await db.Features.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        (await db.Trees.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct)).Select(x => x.ToDto()).ToList());

    private static async Task<bool> UsesIdsOfOtherSitesAsync(Guid siteId, SiteLayoutDto layout, ComplotDbContext db, CancellationToken ct)
    {
        var ids = layout.Plots.Select(x => x.Id).Concat(layout.Beds.Select(x => x.Id)).Concat(layout.Paths.Select(x => x.Id))
            .Concat(layout.Features.Select(x => x.Id)).Concat(layout.Trees.Select(x => x.Id)).ToList();
        return await db.Plots.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Beds.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Paths.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Features.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Trees.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
