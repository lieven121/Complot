using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Catalog;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Catalog.Inventory;

public sealed record InventoryLotDto(
    Guid Id, Guid VarietyId, Guid SupplierId, decimal Quantity, string Unit,
    DateOnly BoughtOn, DateOnly? BestBefore, LotStatus Status, string? Notes);

public sealed record InventoryLotInput(Guid VarietyId, Guid SupplierId, decimal Quantity, string Unit, DateOnly BoughtOn, DateOnly? BestBefore, string? Notes);

public sealed class InventoryLotInputValidator : AbstractValidator<InventoryLotInput>
{
    public InventoryLotInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.VarietyId).MustAsync((id, ct) => db.Varieties.ExistsAsync(id, ct)).WithErrorCode("unknownVariety");
        RuleFor(x => x.SupplierId).MustAsync((id, ct) => db.Suppliers.ExistsAsync(id, ct)).WithErrorCode("unknownSupplier");
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(30);
        RuleFor(x => x.BestBefore).GreaterThanOrEqualTo(x => x.BoughtOn).When(x => x.BestBefore is not null);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public static class InventoryLotEndpoints
{
    public static void MapInventoryLots(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/inventory-lots").WithTags("InventoryLots");
        group.MapGet("/", List).WithName("ListInventoryLots");
        group.MapPost("/", Create).WithName("CreateInventoryLot").Validate<InventoryLotInput>();
        group.MapPut("/{id:guid}", Update).WithName("UpdateInventoryLot").Validate<InventoryLotInput>();
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteInventoryLot");
    }

    private static InventoryLotDto ToDto(this InventoryLot l, DateOnly today) =>
        new(l.Id, l.VarietyId, l.SupplierId, l.Quantity, l.Unit, l.BoughtOn, l.BestBefore, l.StatusOn(today), l.Notes);

    private static void Apply(this InventoryLotInput input, InventoryLot lot)
    {
        lot.VarietyId = input.VarietyId;
        lot.SupplierId = input.SupplierId;
        lot.Quantity = input.Quantity;
        lot.Unit = input.Unit.Trim();
        lot.BoughtOn = input.BoughtOn;
        lot.BestBefore = input.BestBefore;
        lot.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
    }

    private static async Task<InventoryLotDto[]> List(Guid? varietyId, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var query = db.InventoryLots.AsNoTracking();
        if (varietyId is { } id) query = query.Where(l => l.VarietyId == id);
        var today = time.Today();
        return (await query.OrderBy(l => l.BestBefore).ToListAsync(ct)).Select(l => l.ToDto(today)).ToArray();
    }

    private static async Task<Created<InventoryLotDto>> Create(InventoryLotInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var lot = new InventoryLot { Unit = "" };
        input.Apply(lot);
        db.InventoryLots.Add(lot);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/inventory-lots/{lot.Id}", lot.ToDto(time.Today()));
    }

    private static async Task<Results<Ok<InventoryLotDto>, ProblemHttpResult>> Update(Guid id, InventoryLotInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.InventoryLots.FindAsync([id], ct) is not { } lot) return ApiProblems.NotFound();
        input.Apply(lot);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(lot.ToDto(time.Today()));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.InventoryLots.FindAsync([id], ct) is not { } lot) return ApiProblems.NotFound();
        db.InventoryLots.Remove(lot);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }
}
