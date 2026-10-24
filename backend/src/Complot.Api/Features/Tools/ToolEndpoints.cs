using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Api.Features.Plantings;
using Complot.Domain.Tools;
using Complot.Domain.Work;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Tools;

public sealed record ToolLogEntryDto(Guid Id, DateOnly Date, string? Text, decimal? Cost, bool IsMaintenance, bool FromTask);

/// <summary>A tool with its computed dates: last and next maintenance, and when it should be replaced.</summary>
public sealed record ToolDto(
    Guid Id, string Name, string Kind, string Icon, string Unit, int QtyGood, int QtyRepair, int QtyWorn, string? Material, bool IsReused,
    DateOnly? BoughtOn, decimal? UnitPrice, string? SupplierName, int? LifespanYears, int? MaintenanceIntervalMonths, int MaintenanceMinutes,
    List<Guid> BedIds, string? Notes, DateOnly? LastMaintenance, DateOnly? NextMaintenance, DateOnly? ReplaceBy, List<ToolLogEntryDto> Log);

public sealed record ToolInput(
    string Name, string Kind, string Icon, string Unit, int QtyGood, int QtyRepair, int QtyWorn, string? Material, bool IsReused,
    DateOnly? BoughtOn, decimal? UnitPrice, string? SupplierName, int? LifespanYears, int? MaintenanceIntervalMonths, int MaintenanceMinutes,
    List<Guid> BedIds, string? Notes);

public sealed record ToolLogInput(DateOnly Date, string? Text, decimal? Cost, bool IsMaintenance);

/// <summary>Peak need of a tool in a year, from the plan (<see cref="Month"/> 1–12).</summary>
public sealed record ToolDemandDto(Guid ToolId, int Quantity, int Month);

public sealed class ToolInputValidator : AbstractValidator<ToolInput>
{
    public ToolInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Kind).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Icon).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(40);
        RuleFor(x => x.QtyGood).InclusiveBetween(0, 100_000);
        RuleFor(x => x.QtyRepair).InclusiveBetween(0, 100_000);
        RuleFor(x => x.QtyWorn).InclusiveBetween(0, 100_000);
        RuleFor(x => x.Material).MaximumLength(100);
        RuleFor(x => x.UnitPrice).InclusiveBetween(0, 1_000_000).When(x => x.UnitPrice is not null);
        RuleFor(x => x.SupplierName).MaximumLength(100);
        RuleFor(x => x.LifespanYears).InclusiveBetween(1, 100).When(x => x.LifespanYears is not null);
        RuleFor(x => x.MaintenanceIntervalMonths).InclusiveBetween(1, 120).When(x => x.MaintenanceIntervalMonths is not null);
        RuleFor(x => x.MaintenanceMinutes).InclusiveBetween(5, 1440);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleForEach(x => x.BedIds).MustAsync((id, ct) => db.Beds.ExistsAsync(id, ct)).WithErrorCode("unknownBed");
    }
}

public sealed class ToolLogInputValidator : AbstractValidator<ToolLogInput>
{
    public ToolLogInputValidator()
    {
        RuleFor(x => x.Text).MaximumLength(500);
        RuleFor(x => x.Text).NotEmpty().When(x => !x.IsMaintenance);
        RuleFor(x => x.Cost).InclusiveBetween(0, 1_000_000).When(x => x.Cost is not null);
    }
}

public static class ToolEndpoints
{
    /// <summary>Tools are shared by all sites: everyone reads, editors change (the group carries that rule).</summary>
    public static void MapTools(this IEndpointRouteBuilder api)
    {
        var tools = api.MapGroup("/tools").WithTags("Tools");
        tools.MapGet("/", List).WithName("ListTools");
        tools.MapGet("/demand", Demand).WithName("GetToolDemand");
        tools.MapPost("/", Create).WithName("CreateTool").Validate<ToolInput>();
        tools.MapPut("/{id:guid}", Update).WithName("UpdateTool").Validate<ToolInput>();
        tools.MapDelete("/{id:guid}", Delete).WithName("DeleteTool");
        tools.MapPost("/{id:guid}/log", AddLog).WithName("AddToolLog").Validate<ToolLogInput>();
        tools.MapDelete("/{id:guid}/log/{entryId:guid}", DeleteLog).WithName("DeleteToolLog");
    }

    private static ToolDto ToDto(Tool t) => new(
        t.Id, t.Name, t.Kind, t.Icon, t.Unit, t.QtyGood, t.QtyRepair, t.QtyWorn, t.Material, t.IsReused, t.BoughtOn, t.UnitPrice, t.SupplierName,
        t.LifespanYears, t.MaintenanceIntervalMonths, t.MaintenanceMinutes, t.BedIds, t.Notes, t.LastMaintenance, t.NextMaintenance, t.ReplaceBy,
        t.Log.OrderByDescending(e => e.Date).Select(e => new ToolLogEntryDto(e.Id, e.Date, e.Text, e.Cost, e.IsMaintenance, e.TaskKey is not null)).ToList());

    private static async Task<ToolDto[]> List(ComplotDbContext db, CancellationToken ct) =>
        (await db.Tools.AsNoTracking().ToListAsync(ct)).OrderBy(t => t.Kind).ThenBy(t => t.Name).Select(ToDto).ToArray();

    /// <summary>Peak need per tool in <paramref name="year"/>, from the plantings of the sites the caller can see.</summary>
    private static async Task<ToolDemandDto[]> Demand(int year, ComplotDbContext db, CurrentUser user, CancellationToken ct)
    {
        var visible = await db.VisibleSiteIdsAsync(await user.GetAsync(ct), ct);
        var (from, to) = (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        var plantings = await db.Plantings.AsNoTracking().WithRuleData()
            .Where(p => visible.Contains(p.Bed.SiteId) && p.SowOn <= to && (p.ClearOn == null || p.ClearOn >= from))
            .ToListAsync(ct);
        return ToolDemand.PeaksIn(plantings, year).Select(p => new ToolDemandDto(p.ToolId, p.Quantity, p.Month)).ToArray();
    }

    private static async Task<Created<ToolDto>> Create(ToolInput input, ComplotDbContext db, CancellationToken ct)
    {
        var tool = new Tool { Name = "", Kind = "" };
        Apply(input, tool);
        db.Tools.Add(tool);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/tools/{tool.Id}", ToDto(tool));
    }

    private static async Task<Results<Ok<ToolDto>, ProblemHttpResult>> Update(Guid id, ToolInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Tools.FindAsync([id], ct) is not { } tool) return ApiProblems.NotFound();
        Apply(input, tool);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(tool));
    }

    /// <summary>Also drops the tool from the varieties that need it.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Tools.FindAsync([id], ct) is not { } tool) return ApiProblems.NotFound();
        foreach (var variety in (await db.Varieties.ToListAsync(ct)).Where(v => v.ToolNeeds.Any(n => n.ToolId == id)))
            variety.ToolNeeds = variety.ToolNeeds.Where(n => n.ToolId != id).ToList();
        db.Tools.Remove(tool);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<ToolDto>, ProblemHttpResult>> AddLog(Guid id, ToolLogInput input, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Tools.FindAsync([id], ct) is not { } tool) return ApiProblems.NotFound();
        tool.Log.Add(new ToolLogEntry { Date = input.Date, Text = Clean(input.Text), Cost = input.Cost, IsMaintenance = input.IsMaintenance });
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(tool));
    }

    private static async Task<Results<Ok<ToolDto>, ProblemHttpResult>> DeleteLog(Guid id, Guid entryId, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.Tools.FindAsync([id], ct) is not { } tool) return ApiProblems.NotFound();
        if (tool.Log.RemoveAll(e => e.Id == entryId) == 0) return ApiProblems.NotFound();
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(tool));
    }

    private static void Apply(ToolInput input, Tool tool)
    {
        tool.Name = input.Name.Trim();
        tool.Kind = input.Kind.Trim();
        tool.Icon = input.Icon.Trim();
        tool.Unit = input.Unit.Trim();
        tool.QtyGood = input.QtyGood;
        tool.QtyRepair = input.QtyRepair;
        tool.QtyWorn = input.QtyWorn;
        tool.Material = Clean(input.Material);
        tool.IsReused = input.IsReused;
        tool.BoughtOn = input.BoughtOn;
        tool.UnitPrice = input.UnitPrice;
        tool.SupplierName = Clean(input.SupplierName);
        tool.LifespanYears = input.LifespanYears;
        tool.MaintenanceIntervalMonths = input.MaintenanceIntervalMonths;
        tool.MaintenanceMinutes = input.MaintenanceMinutes;
        tool.BedIds = input.BedIds.Distinct().ToList();
        tool.Notes = Clean(input.Notes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Checking off a maintenance task logs the maintenance (which moves the next due date); reopening removes it.</summary>
public static class ToolLog
{
    public static async Task FollowTaskAsync(ComplotDbContext db, GardenTask task, TaskState state, CancellationToken ct)
    {
        if (task.Kind != TaskKind.Maintenance || TaskKeys.Parse(task.Key) is not ("tool", var toolId)) return;
        if (await db.Tools.FindAsync([toolId], ct) is not { } tool) return;
        var entry = tool.Log.FirstOrDefault(e => e.TaskKey == task.Key);
        if (state.Done && entry is null)
            tool.Log.Add(new ToolLogEntry { Date = state.DoneOn ?? task.Date, IsMaintenance = true, TaskKey = task.Key });
        else if (state.Done && entry is not null)
            entry.Date = state.DoneOn ?? entry.Date;
        else if (!state.Done && entry is not null)
            tool.Log.Remove(entry);
    }
}
