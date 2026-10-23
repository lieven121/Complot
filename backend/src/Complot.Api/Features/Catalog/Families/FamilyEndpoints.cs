using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Catalog;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Catalog.Families;

public sealed record FamilyDto(Guid Id, string LatinName, string NameNl, string NameEn, int RotationRestYears);

public sealed record FamilyInput(string LatinName, string NameNl, string NameEn, int RotationRestYears);

public sealed class FamilyInputValidator : AbstractValidator<FamilyInput>
{
    public FamilyInputValidator()
    {
        RuleFor(x => x.LatinName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameNl).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RotationRestYears).InclusiveBetween(0, 10);
    }
}

public static class FamilyEndpoints
{
    public static void MapFamilies(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/families").WithTags("Families");
        group.MapGet("/", List).WithName("ListFamilies");
        group.MapPost("/", Create).WithName("CreateFamily").Validate<FamilyInput>();
        group.MapPut("/{id:guid}", Update).WithName("UpdateFamily").Validate<FamilyInput>();
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteFamily");
    }

    public static FamilyDto ToDto(this PlantFamily f) => new(f.Id, f.LatinName, f.NameNl, f.NameEn, f.RotationRestYears);

    private static void Apply(this FamilyInput input, PlantFamily family)
    {
        family.LatinName = input.LatinName.Trim();
        family.NameNl = input.NameNl.Trim();
        family.NameEn = input.NameEn.Trim();
        family.RotationRestYears = input.RotationRestYears;
    }

    private static async Task<FamilyDto[]> List(ComplotDbContext db, CancellationToken ct) =>
        (await db.Families.AsNoTracking().OrderBy(f => f.LatinName).ToListAsync(ct)).Select(ToDto).ToArray();

    private static async Task<Results<Created<FamilyDto>, ProblemHttpResult>> Create(FamilyInput input, ComplotDbContext db, CancellationToken ct)
    {
        var family = new PlantFamily { LatinName = "", NameNl = "", NameEn = "" };
        input.Apply(family);
        db.Families.Add(family);
        if (await db.SaveOrConflictAsync("family.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Created($"/api/families/{family.Id}", family.ToDto());
    }

    private static async Task<Results<Ok<FamilyDto>, ProblemHttpResult>> Update(Guid id, FamilyInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Families.FindAsync([id], ct) is not { } family) return ApiProblems.NotFound();
        input.Apply(family);
        if (await db.SaveOrConflictAsync("family.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Ok(family.ToDto());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Families.FindAsync([id], ct) is not { } family) return ApiProblems.NotFound();
        var inUse = await db.Categories.AnyAsync(c => c.DefaultFamilyId == id, ct) || await db.Varieties.AnyAsync(v => v.FamilyId == id, ct);
        if (inUse) return ApiProblems.Conflict("family.inUse");
        db.Families.Remove(family);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
