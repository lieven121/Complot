using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Catalog;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Catalog.Categories;

public sealed record CategoryDto(
    Guid Id, string Slug, string Name, string Icon, string Color, Guid? DefaultFamilyId, bool RotationExempt,
    IReadOnlyList<Guid> GoodNeighbourIds, IReadOnlyList<Guid> BadNeighbourIds, int VarietyCount);

public sealed record CategoryInput(
    string Slug, string Name, string Icon, string Color, Guid? DefaultFamilyId, bool RotationExempt,
    List<Guid> GoodNeighbourIds, List<Guid> BadNeighbourIds);

public sealed class CategoryInputValidator : AbstractValidator<CategoryInput>
{
    public CategoryInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(60).Matches("^[a-z0-9]+(-[a-z0-9]+)*$").WithErrorCode("slug");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Color).NotEmpty().Matches("^#[0-9a-fA-F]{6}$").WithErrorCode("color");
        RuleFor(x => x.DefaultFamilyId)
            .MustAsync((id, ct) => db.Families.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownFamily")
            .When(x => x.DefaultFamilyId is not null);
        RuleForEach(x => x.GoodNeighbourIds.Concat(x.BadNeighbourIds))
            .MustAsync((id, ct) => db.Categories.ExistsAsync(id, ct)).WithErrorCode("unknownCategory")
            .OverridePropertyName("neighbourIds");
        RuleFor(x => x)
            .Must(x => !x.GoodNeighbourIds.Intersect(x.BadNeighbourIds).Any()).WithErrorCode("neighbourBothGoodAndBad")
            .OverridePropertyName("badNeighbourIds");
    }
}

public static class CategoryEndpoints
{
    public static void MapCategories(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/categories").WithTags("Categories");
        group.MapGet("/", List).WithName("ListCategories");
        group.MapPost("/", Create).WithName("CreateCategory").Validate<CategoryInput>();
        group.MapPut("/{id:guid}", Update).WithName("UpdateCategory").Validate<CategoryInput>();
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteCategory");
    }

    public static CategoryDto ToDto(this PlantCategory c, int varietyCount) => new(
        c.Id, c.Slug, c.Name, c.Icon, c.Color, c.DefaultFamilyId, c.RotationExempt,
        c.CompanionIds(CompanionRelation.Good).ToArray(), c.CompanionIds(CompanionRelation.Bad).ToArray(), varietyCount);

    public static void Apply(this CategoryInput input, PlantCategory category)
    {
        category.Slug = input.Slug.Trim();
        category.Name = input.Name.Trim();
        category.Icon = input.Icon.Trim();
        category.Color = input.Color.ToLowerInvariant();
        category.DefaultFamilyId = input.DefaultFamilyId;
        category.RotationExempt = input.RotationExempt;
        var companions = input.GoodNeighbourIds.Select(id => (id, CompanionRelation.Good))
            .Concat(input.BadNeighbourIds.Select(id => (id, CompanionRelation.Bad)))
            .Where(c => c.id != category.Id)
            .DistinctBy(c => c.id);
        category.Companions.SyncWith(companions, c => c.OtherCategoryId, c => c.id,
            c => new CategoryCompanion { CategoryId = category.Id, OtherCategoryId = c.id, Relation = c.Item2 },
            (c, existing) => existing.Relation = c.Item2);
    }

    private static async Task<CategoryDto[]> List(ComplotDbContext db, CancellationToken ct)
    {
        var counts = await db.Varieties.GroupBy(v => v.CategoryId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        return categories.Select(c => c.ToDto(counts.GetValueOrDefault(c.Id))).ToArray();
    }

    private static async Task<Results<Created<CategoryDto>, ProblemHttpResult>> Create(CategoryInput input, ComplotDbContext db, CancellationToken ct)
    {
        var category = new PlantCategory { Slug = "", Name = "" };
        input.Apply(category);
        db.Categories.Add(category);
        if (await db.SaveOrConflictAsync("category.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Created($"/api/categories/{category.Id}", category.ToDto(0));
    }

    private static async Task<Results<Ok<CategoryDto>, ProblemHttpResult>> Update(Guid id, CategoryInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Categories.FindAsync([id], ct) is not { } category) return ApiProblems.NotFound();
        input.Apply(category);
        if (await db.SaveOrConflictAsync("category.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Ok(category.ToDto(await db.Varieties.CountAsync(v => v.CategoryId == id, ct)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Categories.FindAsync([id], ct) is not { } category) return ApiProblems.NotFound();
        if (await db.Varieties.AnyAsync(v => v.CategoryId == id, ct)) return ApiProblems.Conflict("category.inUse");
        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
