using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Api.Features.Plantings;
using Complot.Domain.Access;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Garden;

public static class SiteEndpoints
{
    public static IServiceCollection AddGarden(this IServiceCollection services) => services.AddScoped<SiteDeletion>();

    public static void MapSites(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/sites").WithTags("Sites");
        group.MapGet("/", List).WithName("ListSites");
        group.MapPost("/", Create).WithName("CreateSite").Validate<SiteInput>().RequireAdmin();
        group.MapPut("/{id:guid}", Update).WithName("UpdateSite").Validate<SiteInput>().RequireAdmin();
        group.MapPost("/{id:guid}/archive", (Guid id, ComplotDbContext db, CurrentUser user, TimeProvider time, CancellationToken ct) =>
            SetArchived(id, time.GetUtcNow().UtcDateTime, db, user, ct)).WithName("ArchiveSite").RequireAdmin();
        group.MapPost("/{id:guid}/restore", (Guid id, ComplotDbContext db, CurrentUser user, CancellationToken ct) =>
            SetArchived(id, null, db, user, ct)).WithName("RestoreSite").RequireAdmin();
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteSite").RequireAdmin();
        group.MapGet("/{id:guid}/layout", GetLayout).WithName("GetSiteLayout").RequireSite(SiteRole.View, SiteResolvers.Route());
        group.MapPut("/{id:guid}/layout", SaveLayout).WithName("SaveSiteLayout").Validate<SiteLayoutDto>()
            .RequireSite(SiteRole.Edit, SiteResolvers.Route());
    }

    /// <summary>The sites the caller can see; archived ones only on request, for admins (the admin page).</summary>
    private static async Task<SiteDto[]> List(bool? includeArchived, ComplotDbContext db, CurrentUser current, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var withArchived = includeArchived == true && user.IsAdmin;
        var sites = await db.Sites.AsNoTracking()
            .Where(s => withArchived || s.ArchivedAt == null)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
        var visible = sites.Where(s => user.CanView(s.Id, s.IsArchived)).ToList();
        var ids = visible.Select(s => s.Id).ToList();
        var beds = await db.Beds.Where(b => ids.Contains(b.SiteId)).GroupBy(b => b.SiteId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var members = await db.SiteAccess.Where(a => ids.Contains(a.SiteId)).GroupBy(a => a.SiteId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return visible.Select(s => ToDto(s, user, beds.GetValueOrDefault(s.Id), members.GetValueOrDefault(s.Id))).ToArray();
    }

    private static SiteDto ToDto(Site site, UserAccess user, int bedCount, int memberCount) => new(
        site.Id, site.Name, site.Notes, bedCount, memberCount, site.IsArchived, user.CanEdit(site.Id, site.IsArchived) ? SiteRole.Edit : SiteRole.View);

    private static async Task<SiteDto> ToDtoAsync(Site site, ComplotDbContext db, CurrentUser current, CancellationToken ct) => ToDto(
        site, await current.GetAsync(ct), await db.Beds.CountAsync(b => b.SiteId == site.Id, ct), await db.SiteAccess.CountAsync(a => a.SiteId == site.Id, ct));

    private static async Task<Created<SiteDto>> Create(SiteInput input, ComplotDbContext db, CurrentUser current, CancellationToken ct)
    {
        var site = new Site { Name = input.Name.Trim(), Notes = Clean(input.Notes) };
        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/sites/{site.Id}", await ToDtoAsync(site, db, current, ct));
    }

    private static async Task<Results<Ok<SiteDto>, ProblemHttpResult>> Update(Guid id, SiteInput input, ComplotDbContext db, CurrentUser current, CancellationToken ct)
    {
        if (await db.Sites.FindAsync([id], ct) is not { } site) return ApiProblems.NotFound();
        site.Name = input.Name.Trim();
        site.Notes = Clean(input.Notes);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(site, db, current, ct));
    }

    private static async Task<Results<Ok<SiteDto>, ProblemHttpResult>> SetArchived(
        Guid id, DateTime? archivedAt, ComplotDbContext db, CurrentUser current, CancellationToken ct)
    {
        if (await db.Sites.FindAsync([id], ct) is not { } site) return ApiProblems.NotFound();
        site.ArchivedAt = archivedAt;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ToDtoAsync(site, db, current, ct));
    }

    /// <summary>Only an archived site can be deleted, and only with its exact name as confirmation; it removes everything.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(
        Guid id, string? confirmName, ComplotDbContext db, SiteDeletion deletion, CancellationToken ct)
    {
        if (await db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct) is not { } site) return ApiProblems.NotFound();
        if (!site.IsArchived) return ApiProblems.Conflict("site.notArchived");
        if (confirmName?.Trim() != site.Name) return ApiProblems.Conflict("site.confirmName");
        await deletion.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<SiteLayoutDto>, ProblemHttpResult>> GetLayout(
        Guid id, ComplotDbContext db, TimeProvider time, CancellationToken ct) =>
        await db.Sites.AnyAsync(s => s.Id == id, ct) ? TypedResults.Ok(await LoadLayout(id, db, time, ct)) : ApiProblems.NotFound();

    private static async Task<Results<Ok<SiteLayoutDto>, ProblemHttpResult>> SaveLayout(
        Guid id, SiteLayoutDto layout, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!await db.Sites.AnyAsync(s => s.Id == id, ct)) return ApiProblems.NotFound();

        var plots = await db.Plots.Where(x => x.SiteId == id).ToListAsync(ct);
        var beds = await db.Beds.Where(x => x.SiteId == id).ToListAsync(ct);
        var paths = await db.Paths.Where(x => x.SiteId == id).ToListAsync(ct);
        var features = await db.Features.Where(x => x.SiteId == id).ToListAsync(ct);
        var trees = await db.Trees.Where(x => x.SiteId == id).ToListAsync(ct);
        var markers = await db.Markers.Where(x => x.SiteId == id).ToListAsync(ct);

        if (await UsesIdsOfOtherSitesAsync(id, layout, db, ct)) return ApiProblems.Conflict("layout.foreignId");
        var removedBedIds = beds.Select(b => b.Id).Except(layout.Beds.Select(b => b.Id)).ToList();
        if (await BedUsage.AnyInUseAsync(db, removedBedIds, ct)) return ApiProblems.Conflict("bed.inUse");
        var kindChangedIds = beds.Where(b => layout.Beds.Any(x => x.Id == b.Id && x.Kind != b.Kind)).Select(b => b.Id).ToList();
        if (await BedUsage.AnyInUseAsync(db, kindChangedIds, ct)) return ApiProblems.Conflict("bed.kindInUse");

        db.Plots.SyncById(plots, layout.Plots, p => p.Id,
            p => new Plot { Id = p.Id, SiteId = id, Name = "", Shape = p.Shape.ToDomain() },
            (p, e) => { e.Name = p.Name.Trim(); e.Type = p.Type; e.Shape = p.Shape.ToDomain(); });
        db.Beds.SyncById(beds, layout.Beds, b => b.Id,
            b => new Bed { Id = b.Id, SiteId = id, Code = "", Shape = b.Shape.ToDomain() },
            (b, e) => { e.PlotId = b.PlotId; e.Code = b.Code.Trim(); e.Kind = b.Kind; e.Shape = b.Shape.ToDomain(); e.Sun = b.Sun; e.Soil = Clean(b.Soil); e.Notes = Clean(b.Notes); });
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
                e.Pollination = t.Pollination.ToDomain();
            });
        db.Markers.SyncById(markers, layout.Markers, m => m.Id,
            m => new MapMarker { Id = m.Id, SiteId = id, Color = m.Color },
            (m, e) =>
            {
                e.Kind = m.Kind;
                e.Label = Clean(m.Label);
                e.Color = m.Color;
                (e.Outline, e.X, e.Y, e.Width, e.Height) = (m.Shape.Kind, m.Shape.X, m.Shape.Y, m.Shape.Width, m.Shape.Height);
                e.Thickness = m.Thickness;
            });

        if (await db.SaveOrConflictAsync("bed.duplicateCode", ct) is { } conflict) return conflict;
        return TypedResults.Ok(await LoadLayout(id, db, time, ct));
    }

    public static async Task<SiteLayoutDto> LoadLayout(Guid siteId, ComplotDbContext db, TimeProvider time, CancellationToken ct) => new(
        (await db.Plots.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        (await db.Beds.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Code).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        (await db.Paths.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        (await db.Features.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct)).Select(x => x.ToDto()).ToList(),
        await LoadTrees(siteId, db, time, ct),
        (await db.Markers.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Label).ToListAsync(ct)).Select(x => x.ToDto()).ToList());

    /// <summary>The site's trees with the trees and plantings (from today on) that pollinate them.</summary>
    private static async Task<List<TreeDto>> LoadTrees(Guid siteId, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var trees = await db.Trees.AsNoTracking().Where(x => x.SiteId == siteId).OrderBy(x => x.Name).ToListAsync(ct);
        if (trees.All(t => t.Pollination is null)) return trees.Select(t => t.ToDto()).ToList();

        var today = time.Today();
        var plantings = (await db.ForSiteAsync(siteId, ct)).Where(p => p.EndInBed >= today);
        var all = PollinationCheck.Collect(trees, plantings);
        return trees.Select(t => t.ToDto(Flowering.Of(t) is { } f
            ? PollinationCheck.PollinatorsOf(f, all)?.Select(x => new PollinatorDto(x.Partner.Id, x.Partner.Name, Math.Round(x.Distance, 1))).ToList()
            : null)).ToList();
    }

    private static async Task<bool> UsesIdsOfOtherSitesAsync(Guid siteId, SiteLayoutDto layout, ComplotDbContext db, CancellationToken ct)
    {
        var ids = layout.Plots.Select(x => x.Id).Concat(layout.Beds.Select(x => x.Id)).Concat(layout.Paths.Select(x => x.Id))
            .Concat(layout.Features.Select(x => x.Id)).Concat(layout.Trees.Select(x => x.Id)).Concat(layout.Markers.Select(x => x.Id)).ToList();
        return await db.Plots.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Beds.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Paths.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Features.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Trees.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct)
               || await db.Markers.AnyAsync(x => ids.Contains(x.Id) && x.SiteId != siteId, ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
