using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Catalog;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Catalog.Suppliers;

public sealed record SupplierDto(Guid Id, string Name, string? Url, bool IsOwnSource, string? Notes);

public sealed record SupplierInput(string Name, string? Url, bool IsOwnSource, string? Notes);

public sealed class SupplierInputValidator : AbstractValidator<SupplierInput>
{
    public SupplierInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Url).MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https").WithErrorCode("url")
            .When(x => !string.IsNullOrWhiteSpace(x.Url));
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public static class SupplierEndpoints
{
    public static void MapSuppliers(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/suppliers").WithTags("Suppliers");
        group.MapGet("/", List).WithName("ListSuppliers");
        group.MapPost("/", Create).WithName("CreateSupplier").Validate<SupplierInput>();
        group.MapPut("/{id:guid}", Update).WithName("UpdateSupplier").Validate<SupplierInput>();
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteSupplier");
    }

    public static SupplierDto ToDto(this Supplier s) => new(s.Id, s.Name, s.Url, s.IsOwnSource, s.Notes);

    private static void Apply(this SupplierInput input, Supplier supplier)
    {
        supplier.Name = input.Name.Trim();
        supplier.Url = string.IsNullOrWhiteSpace(input.Url) ? null : input.Url.Trim();
        supplier.IsOwnSource = input.IsOwnSource;
        supplier.Notes = input.Notes;
    }

    private static async Task<SupplierDto[]> List(ComplotDbContext db, CancellationToken ct) =>
        (await db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct)).Select(ToDto).ToArray();

    private static async Task<Results<Created<SupplierDto>, ProblemHttpResult>> Create(SupplierInput input, ComplotDbContext db, CancellationToken ct)
    {
        var supplier = new Supplier { Name = "" };
        input.Apply(supplier);
        db.Suppliers.Add(supplier);
        if (await db.SaveOrConflictAsync("supplier.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Created($"/api/suppliers/{supplier.Id}", supplier.ToDto());
    }

    private static async Task<Results<Ok<SupplierDto>, ProblemHttpResult>> Update(Guid id, SupplierInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Suppliers.FindAsync([id], ct) is not { } supplier) return ApiProblems.NotFound();
        input.Apply(supplier);
        if (await db.SaveOrConflictAsync("supplier.duplicate", ct) is { } conflict) return conflict;
        return TypedResults.Ok(supplier.ToDto());
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Suppliers.FindAsync([id], ct) is not { } supplier) return ApiProblems.NotFound();
        var inUse = await db.InventoryLots.AnyAsync(l => l.SupplierId == id, ct)
                    || await db.Varieties.AnyAsync(v => v.Suppliers.Any(s => s.SupplierId == id), ct);
        if (inUse) return ApiProblems.Conflict("supplier.inUse");
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
