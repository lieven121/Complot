using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using Complot.Domain.Access;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Plantings;

public static class PlantingEndpoints
{
    public static void MapPlantings(this IEndpointRouteBuilder api)
    {
        var site = SiteResolvers.Route("siteId");
        api.MapGet("/sites/{siteId:guid}/plantings", List).WithTags("Plantings").WithName("ListPlantings").RequireSite(SiteRole.View, site);
        api.MapGet("/sites/{siteId:guid}/demand", Demand).WithTags("Plantings").WithName("GetPlantDemand").RequireSite(SiteRole.View, site);
        api.MapGet("/sites/{siteId:guid}/checks", Checks).WithTags("Plantings").WithName("GetSiteChecks").RequireSite(SiteRole.View, site);

        var bed = SiteResolvers.BedInBody<PlanPlantingInput>(x => x.BedId);
        var group = api.MapGroup("/plantings").WithTags("Plantings");
        group.MapPost("/preview", Preview).WithName("PreviewPlanting").Validate<PlanPlantingInput>().RequireSite(SiteRole.Edit, bed);
        group.MapPost("/", Create).WithName("CreatePlanting").Validate<PlanPlantingInput>().RequireSite(SiteRole.Edit, bed);
        group.MapPut("/{id:guid}", Update).WithName("UpdatePlanting").Validate<UpdatePlantingInput>()
            .RequireSite(SiteRole.Edit, SiteResolvers.Planting);
        group.MapDelete("/{id:guid}", Delete).WithName("DeletePlanting").RequireSite(SiteRole.Edit, SiteResolvers.Planting);
    }

    /// <summary>Plantings with everything the rules need (bed geometry, variety, category companions, families).</summary>
    public static IQueryable<Planting> WithRuleData(this IQueryable<Planting> query) => query
        .Include(p => p.Bed)
        .Include(p => p.Variety).ThenInclude(v => v.Category).ThenInclude(c => c.DefaultFamily)
        .Include(p => p.Variety).ThenInclude(v => v.Family);

    public static Task<List<Planting>> ForSiteAsync(this ComplotDbContext db, Guid siteId, CancellationToken ct) =>
        db.Plantings.AsNoTracking().WithRuleData().Where(p => p.Bed.SiteId == siteId).ToListAsync(ct);

    /// <summary>Default period: from the start of last year to the end of next year.</summary>
    private static (DateOnly From, DateOnly To) Period(DateOnly? from, DateOnly? to, TimeProvider time)
    {
        var year = time.Today().Year;
        return (from ?? new DateOnly(year - 1, 1, 1), to ?? new DateOnly(year + 1, 12, 31));
    }

    private static async Task<PlantingDto[]> List(Guid siteId, DateOnly? from, DateOnly? to, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var (start, end) = Period(from, to, time);
        var plantings = await db.ForSiteAsync(siteId, ct);
        return plantings
            .Where(p => p.SowOn <= end && p.EndInBed >= start)
            .OrderBy(p => p.SowOn)
            .Select(p => p.ToDto(start, end))
            .ToArray();
    }

    private static async Task<PlantingWarningDto[]> Checks(Guid siteId, ComplotDbContext db, CancellationToken ct) =>
        PlantingChecks.Check(await db.ForSiteAsync(siteId, ct), await db.TreesOfSiteAsync(siteId, ct)).Select(w => w.ToDto()).ToArray();

    /// <summary>Plants needed per variety for the plantings started from <paramref name="from"/> to <paramref name="to"/>, against the stock, with the budget.</summary>
    private static async Task<PlantDemandDto[]> Demand(Guid siteId, DateOnly from, DateOnly to, ComplotDbContext db, CancellationToken ct)
    {
        var plantings = await db.ForSiteAsync(siteId, ct);
        var varietyIds = plantings.Select(p => p.VarietyId).Distinct().ToList();
        var lots = await db.InventoryLots.AsNoTracking().Where(l => varietyIds.Contains(l.VarietyId)).ToListAsync(ct);
        var offers = await db.Varieties.AsNoTracking().Where(v => varietyIds.Contains(v.Id)).SelectMany(v => v.Suppliers).ToListAsync(ct);
        return PlantDemand.Calculate(plantings, lots, from, to, offers).Select(d => d.ToDto()).ToArray();
    }

    private static Task<List<Tree>> TreesOfSiteAsync(this ComplotDbContext db, Guid siteId, CancellationToken ct) =>
        db.Trees.AsNoTracking().Where(t => t.SiteId == siteId).ToListAsync(ct);

    private static async Task<Results<Ok<PlantingPreviewDto>, ProblemHttpResult>> Preview(
        PlanPlantingInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var planting = await BuildAsync(input, db, ct);
        if (SpotProblem(planting) is { } problem) return problem;
        var siteId = planting.Bed.SiteId;
        var all = (await db.ForSiteAsync(siteId, ct)).Append(planting).ToList();
        var warnings = PlantingChecks.Check(all, await db.TreesOfSiteAsync(siteId, ct)).Where(w => w.PlantingId == planting.Id).ToList();
        if (PlantingChecks.OutsideSowingWindow(planting) is { } window) warnings.Insert(0, window);
        var (from, to) = Period(null, null, time);
        return TypedResults.Ok(new PlantingPreviewDto(planting.ToDto(from, to), warnings.Select(w => w.ToDto()).ToList()));
    }

    private static async Task<Results<Created<PlantingDto>, ProblemHttpResult>> Create(
        PlanPlantingInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var planting = await BuildAsync(input, db, ct);
        if (SpotProblem(planting) is { } problem) return problem;
        db.Plantings.Add(planting);
        await db.SaveChangesAsync(ct);
        var (from, to) = Period(null, null, time);
        return TypedResults.Created($"/api/plantings/{planting.Id}", planting.ToDto(from, to));
    }

    private static async Task<Results<Ok<PlantingDto>, ProblemHttpResult>> Update(
        Guid id, UpdatePlantingInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Plantings.WithRuleData().FirstOrDefaultAsync(p => p.Id == id, ct) is not { } planting) return ApiProblems.NotFound();
        planting.Share = input.Share;
        (planting.Count, planting.SpotX, planting.SpotY) = (input.Count, input.SpotX, input.SpotY);
        (planting.SpotColumns, planting.SpotVertical) = (input.SpotColumns, input.SpotVertical);
        if (SpotProblem(planting) is { } problem) return problem;
        planting.ApplyDates(new PlantingDates(input.SowOn, input.PlantOutOn, input.HarvestFrom, input.HarvestTo, input.ClearOn));
        planting.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        await db.SaveChangesAsync(ct);
        var (from, to) = Period(null, null, time);
        return TypedResults.Ok(planting.ToDto(from, to));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Plantings.FindAsync([id], ct) is not { } planting) return ApiProblems.NotFound();
        db.Plantings.Remove(planting);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>A new planting with dates calculated from the variety (not yet added to the context).</summary>
    private static async Task<Planting> BuildAsync(PlanPlantingInput input, ComplotDbContext db, CancellationToken ct)
    {
        var bed = await db.Beds.FirstAsync(b => b.Id == input.BedId, ct);
        var variety = await db.Varieties.Include(v => v.Category).ThenInclude(c => c.DefaultFamily).Include(v => v.Family)
            .FirstAsync(v => v.Id == input.VarietyId, ct);
        var planting = new Planting
        {
            BedId = bed.Id, Bed = bed, VarietyId = variety.Id, Variety = variety, Share = input.Share,
            Count = input.Count, SpotX = input.SpotX, SpotY = input.SpotY, SpotColumns = input.SpotColumns, SpotVertical = input.SpotVertical,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
        };
        planting.ApplyDates(PlantingSchedule.Calculate(variety, input.SowOn, input.Direct));
        return planting;
    }

    /// <summary>400 when the count/position does not fit the kind of bed (see <see cref="Planting.SpotProblem"/>).</summary>
    private static ProblemHttpResult? SpotProblem(Planting planting) =>
        planting.SpotProblem() is var (field, code)
            ? ApiProblems.Validation(new Dictionary<string, ErrorDetail[]> { [field] = [new ErrorDetail(code)] })
            : null;
}
