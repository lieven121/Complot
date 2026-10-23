using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Catalog;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Catalog.Varieties;

public static class VarietyEndpoints
{
    public static void MapVarieties(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/varieties").WithTags("Varieties");
        group.MapGet("/", List).WithName("ListVarieties");
        group.MapGet("/{id:guid}", Get).WithName("GetVariety");
        group.MapPost("/", Create).WithName("CreateVariety").Validate<VarietyInput>();
        group.MapPut("/{id:guid}", Update).WithName("UpdateVariety").Validate<VarietyInput>();
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteVariety");
    }

    /// <summary>Varieties with everything needed for <see cref="VarietyDto"/> (family resolution, supplier names).</summary>
    public static IQueryable<PlantVariety> WithDetails(this IQueryable<PlantVariety> query) => query
        .Include(v => v.Category).ThenInclude(c => c.DefaultFamily)
        .Include(v => v.Family)
        .Include(v => v.Suppliers).ThenInclude(s => s.Supplier);

    private static async Task<VarietyDto[]> List(string? q, Guid? categoryId, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var query = db.Varieties.AsNoTracking().WithDetails();
        if (categoryId is { } category) query = query.Where(v => v.CategoryId == category);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = DbExtensions.ContainsPattern(q.Trim());
            query = query.Where(v => EF.Functions.Like(v.Name, pattern, "\\")
                                     || EF.Functions.Like(v.Cultivar!, pattern, "\\")
                                     || EF.Functions.Like(v.LatinName!, pattern, "\\"));
        }
        var varieties = await query.OrderBy(v => v.Name).ThenBy(v => v.Cultivar).ToListAsync(ct);
        var ids = varieties.Select(v => v.Id).ToList();
        var lots = (await db.InventoryLots.AsNoTracking().Where(l => ids.Contains(l.VarietyId)).ToListAsync(ct)).ToLookup(l => l.VarietyId);
        var today = time.Today();
        return varieties.Select(v => v.ToDto(lots[v.Id], today)).ToArray();
    }

    private static async Task<Results<Ok<VarietyDto>, ProblemHttpResult>> Get(Guid id, ComplotDbContext db, TimeProvider time, CancellationToken ct) =>
        await LoadDto(id, db, time, ct) is { } dto ? TypedResults.Ok(dto) : ApiProblems.NotFound();

    private static async Task<Results<Created<VarietyDto>, ProblemHttpResult>> Create(VarietyInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var variety = new PlantVariety { Name = "", Harvest = new MonthWindow(1, 1) };
        input.Apply(variety);
        db.Varieties.Add(variety);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/varieties/{variety.Id}", (await LoadDto(variety.Id, db, time, ct))!);
    }

    private static async Task<Results<Ok<VarietyDto>, ProblemHttpResult>> Update(Guid id, VarietyInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.Varieties.Include(v => v.Suppliers).FirstOrDefaultAsync(v => v.Id == id, ct) is not { } variety) return ApiProblems.NotFound();
        input.Apply(variety);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok((await LoadDto(id, db, time, ct))!);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Varieties.FindAsync([id], ct) is not { } variety) return ApiProblems.NotFound();
        if (await db.InventoryLots.AnyAsync(l => l.VarietyId == id, ct)) return ApiProblems.Conflict("variety.hasInventory");
        if (await db.Plantings.AnyAsync(p => p.VarietyId == id, ct)) return ApiProblems.Conflict("variety.inUse");
        db.Varieties.Remove(variety);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<VarietyDto?> LoadDto(Guid id, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var variety = await db.Varieties.AsNoTracking().WithDetails().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (variety is null) return null;
        var lots = await db.InventoryLots.AsNoTracking().Where(l => l.VarietyId == id).ToListAsync(ct);
        return variety.ToDto(lots, time.Today());
    }
}
