using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Api.Features.Planner;
using Complot.Api.Features.Tools;
using Complot.Domain.Access;
using Complot.Domain.Work;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Work;

/// <summary>
/// A task with its status. Generated tasks have <see cref="Subject"/> (crop or tree) and a <see cref="Kind"/> the UI
/// turns into a sentence; care and custom tasks carry their own <see cref="Title"/>.
/// </summary>
public sealed record TaskDto(
    string Key, TaskKind Kind, DateOnly Date, Guid? SiteId, Guid? BedId, Guid? TreeId, Guid? PlantingId,
    string? Subject, string? Title, int BaseMinutes, int EstimateMinutes,
    bool Done, DateOnly? DoneOn, int? ActualMinutes, Guid? AssigneeId, int? EstimateOverride, DateTime? ScheduledStart,
    bool CalendarSynced, string? CalendarError);

public sealed record EstimateFactorDto(TaskKind Kind, double Factor, int Samples, int BaseMinutes, bool PerSquareMeter);

/// <summary><see cref="CalendarTitle"/>: the task as the scheduler reads it, used as title of calendar events.</summary>
public sealed record TaskStateInput(Guid? AssigneeId, int? EstimateOverride, DateTime? ScheduledStart, string? CalendarTitle = null);

public sealed record CompleteTaskInput(int? ActualMinutes, DateOnly? DoneOn);

public sealed record ManualTaskInput(Guid? SiteId, Guid? BedId, string Title, DateOnly DueOn, int BaseMinutes, Guid? AssigneeId);

public sealed class TaskStateInputValidator : AbstractValidator<TaskStateInput>
{
    public TaskStateInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.AssigneeId).MustAsync((id, ct) => db.People.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownPerson").When(x => x.AssigneeId is not null);
        RuleFor(x => x.EstimateOverride).InclusiveBetween(1, 1440).When(x => x.EstimateOverride is not null);
        RuleFor(x => x.CalendarTitle).MaximumLength(200);
    }
}

public sealed class CompleteTaskInputValidator : AbstractValidator<CompleteTaskInput>
{
    public CompleteTaskInputValidator() => RuleFor(x => x.ActualMinutes).InclusiveBetween(1, 1440).When(x => x.ActualMinutes is not null);
}

public sealed class ManualTaskInputValidator : AbstractValidator<ManualTaskInput>
{
    public ManualTaskInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.SiteId).MustAsync((id, ct) => db.Sites.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownSite").When(x => x.SiteId is not null);
        RuleFor(x => x.BedId)
            .MustAsync((x, id, ct) => db.Beds.AnyAsync(b => b.Id == id && b.SiteId == x.SiteId, ct)).WithErrorCode("unknownBed")
            .When(x => x.BedId is not null);
        RuleFor(x => x.AssigneeId).MustAsync((id, ct) => db.People.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownPerson").When(x => x.AssigneeId is not null);
    }
}

public static class TaskEndpoints
{
    public static void MapTasks(this IEndpointRouteBuilder api)
    {
        var tasks = api.MapGroup("/tasks").WithTags("Tasks");
        tasks.MapGet("/", List).WithName("ListTasks");
        tasks.MapGet("/estimates", Estimates).WithName("GetTaskEstimates");
        tasks.MapPut("/{key}/state", SetState).WithName("SetTaskState").Validate<TaskStateInput>().RequireSite(SiteRole.Edit, SiteResolvers.TaskKey);
        tasks.MapPost("/{key}/complete", Complete).WithName("CompleteTask").Validate<CompleteTaskInput>()
            .RequireSite(SiteRole.Edit, SiteResolvers.TaskKey);
        tasks.MapPost("/{key}/reopen", Reopen).WithName("ReopenTask").RequireSite(SiteRole.Edit, SiteResolvers.TaskKey);

        var target = SiteResolvers.Body<ManualTaskInput>(x => x.SiteId);
        var manual = api.MapGroup("/manual-tasks").WithTags("Tasks");
        manual.MapPost("/", CreateManual).WithName("CreateManualTask").Validate<ManualTaskInput>().RequireSite(SiteRole.Edit, target);
        manual.MapPut("/{id:guid}", UpdateManual).WithName("UpdateManualTask").Validate<ManualTaskInput>()
            .RequireSite(SiteRole.Edit, SiteResolvers.ManualTask).RequireSite(SiteRole.Edit, target);
        manual.MapDelete("/{id:guid}", DeleteManual).WithName("DeleteManualTask").RequireSite(SiteRole.Edit, SiteResolvers.ManualTask);
    }

    private static TaskDto ToDto(GardenTask t, TaskState? s, IReadOnlyDictionary<TaskKind, TaskEstimates.Factor> factors) => new(
        t.Key, t.Kind, t.Date, t.SiteId, t.BedId, t.TreeId, t.PlantingId, t.Subject, t.Title,
        t.BaseMinutes, TaskEstimates.Estimate(t, s, factors),
        s?.Done ?? false, s?.DoneOn, s?.ActualMinutes, s?.AssigneeId, s?.EstimateOverride, s?.ScheduledStart,
        s?.CalendarEventId is not null, s?.CalendarError);

    /// <summary>
    /// Tasks of the sites the caller can see (or of one of them). Default period: two months back (overdue work) to two
    /// months ahead.
    /// </summary>
    private static async Task<TaskDto[]> List(
        DateOnly? from, DateOnly? to, Guid? siteId, TaskService tasks, ComplotDbContext db, CurrentUser user, TimeProvider time, CancellationToken ct)
    {
        var today = time.Today();
        var (start, end) = (from ?? today.AddDays(-60), to ?? today.AddDays(60));
        var visible = await db.VisibleSiteIdsAsync(await user.GetAsync(ct), ct);
        var sites = siteId is { } id ? visible.Where(v => v == id).ToList() : visible;
        var generated = await tasks.GenerateAsync(start, end, sites, includeSiteless: siteId is null, ct);

        // Also tasks planned in the period although they are due outside it (e.g. sowing done a week early).
        var known = generated.Select(t => t.Key).ToHashSet();
        var (startTime, endTime) = (start.ToDateTime(TimeOnly.MinValue), end.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var scheduledKeys = await db.TaskStates.AsNoTracking()
            .Where(s => s.ScheduledStart >= startTime && s.ScheduledStart < endTime)
            .Select(s => s.SourceKey)
            .ToListAsync(ct);
        foreach (var key in scheduledKeys.Where(k => !known.Contains(k)))
        {
            if (await tasks.FindAsync(key, ct) is { } task && (task.SiteId is { } site ? sites.Contains(site) : siteId is null)) generated.Add(task);
        }

        var keys = generated.Select(t => t.Key).ToList();
        var states = await db.TaskStates.AsNoTracking().Where(s => keys.Contains(s.SourceKey)).ToDictionaryAsync(s => s.SourceKey, ct);
        var factors = await tasks.FactorsAsync(ct);
        return generated.Select(t => ToDto(t, states.GetValueOrDefault(t.Key), factors)).ToArray();
    }

    private static async Task<EstimateFactorDto[]> Estimates(TaskService tasks, CancellationToken ct) =>
        (await tasks.FactorsAsync(ct)).Values
            .Select(f => new EstimateFactorDto(f.Kind, f.Value, f.Samples, TaskGenerator.Base[f.Kind].Minutes, TaskGenerator.Base[f.Kind].PerSquareMeter))
            .ToArray();

    /// <summary>Loads the task and its (possibly new) state, applies <paramref name="change"/> and returns the result.</summary>
    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> UpdateState(
        string key, TaskService tasks, ComplotDbContext db, CalendarSync sync, CancellationToken ct, Action<GardenTask, TaskState> change)
    {
        if (await tasks.FindAsync(key, ct) is not { } task) return ApiProblems.NotFound();
        var state = await db.TaskStates.FirstOrDefaultAsync(s => s.SourceKey == key, ct);
        if (state is null) db.TaskStates.Add(state = new TaskState { SourceKey = key });
        change(task, state);
        await ToolLog.FollowTaskAsync(db, task, state, ct);
        var factors = await tasks.FactorsAsync(ct);
        await sync.SyncAsync(task, state, TaskEstimates.Estimate(task, state, factors), ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(task, state, factors));
    }

    private static Task<Results<Ok<TaskDto>, ProblemHttpResult>> SetState(
        string key, TaskStateInput input, TaskService tasks, ComplotDbContext db, CalendarSync sync, CancellationToken ct) =>
        UpdateState(key, tasks, db, sync, ct, (_, state) =>
        {
            state.AssigneeId = input.AssigneeId;
            state.EstimateOverride = input.EstimateOverride;
            state.ScheduledStart = input.ScheduledStart;
            state.CalendarTitle = input.CalendarTitle?.Trim() ?? state.CalendarTitle;
        });

    private static Task<Results<Ok<TaskDto>, ProblemHttpResult>> Complete(
        string key, CompleteTaskInput input, TaskService tasks, ComplotDbContext db, CalendarSync sync, TimeProvider time, CancellationToken ct) =>
        UpdateState(key, tasks, db, sync, ct, (task, state) => state.Complete(task, input.DoneOn ?? time.Today(), input.ActualMinutes));

    private static Task<Results<Ok<TaskDto>, ProblemHttpResult>> Reopen(string key, TaskService tasks, ComplotDbContext db, CalendarSync sync, CancellationToken ct) =>
        UpdateState(key, tasks, db, sync, ct, (_, state) => state.Reopen());

    private static async Task<Ok<TaskDto>> CreateManual(ManualTaskInput input, TaskService tasks, ComplotDbContext db, CancellationToken ct)
    {
        var task = new ManualTask { Title = "" };
        Apply(input, task);
        db.ManualTasks.Add(task);
        var state = new TaskState { SourceKey = task.SourceKey, AssigneeId = input.AssigneeId };
        db.TaskStates.Add(state);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(TaskGenerator.ForManual(task), state, await tasks.FactorsAsync(ct)));
    }

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> UpdateManual(Guid id, ManualTaskInput input, TaskService tasks, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.ManualTasks.FindAsync([id], ct) is not { } task) return ApiProblems.NotFound();
        Apply(input, task);
        var state = await db.TaskStates.FirstOrDefaultAsync(s => s.SourceKey == task.SourceKey, ct);
        if (state is null) db.TaskStates.Add(state = new TaskState { SourceKey = task.SourceKey });
        state.AssigneeId = input.AssigneeId;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(TaskGenerator.ForManual(task), state, await tasks.FactorsAsync(ct)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteManual(Guid id, ComplotDbContext db, CalendarSync sync, CancellationToken ct)
    {
        if (await db.ManualTasks.FindAsync([id], ct) is not { } task) return ApiProblems.NotFound();
        var states = await db.TaskStates.Where(s => s.SourceKey == task.SourceKey).ToListAsync(ct);
        await sync.RemoveAsync(states, ct);
        db.ManualTasks.Remove(task);
        db.TaskStates.RemoveRange(states);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static void Apply(ManualTaskInput input, ManualTask task)
    {
        task.SiteId = input.SiteId;
        task.BedId = input.BedId;
        task.Title = input.Title.Trim();
        task.DueOn = input.DueOn;
        task.BaseMinutes = input.BaseMinutes;
    }
}
