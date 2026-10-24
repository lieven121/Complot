using Complot.Domain.Common;

namespace Complot.Domain.Work;

/// <summary>Someone who does garden work. Not necessarily a user (e.g. "Vrijwilligers").</summary>
public sealed class Person : Entity
{
    public required string Name { get; set; }
    public string Color { get; set; } = "#3f7d4e";

    /// <summary>Secret part of the person's iCal feed URL (read-only subscription to their scheduled tasks).</summary>
    public string? CalendarFeedToken { get; set; }
}

public enum TaskKind
{
    SowIndoor,
    Sow,
    PlantOut,
    /// <summary>Planting a perennial.</summary>
    Plant,
    Harvest,
    Clear,
    /// <summary>Yearly care of a perennial or tree (pruning, mulching…).</summary>
    Care,
    Custom,
}

/// <summary>A task added by hand. Its status is kept in <see cref="TaskState"/> like generated tasks.</summary>
public sealed class ManualTask : Entity
{
    public Guid? SiteId { get; set; }
    public Guid? BedId { get; set; }
    public required string Title { get; set; }
    public DateOnly DueOn { get; set; }
    public int BaseMinutes { get; set; } = 30;

    public string SourceKey => TaskKeys.Manual(Id);
}

/// <summary>
/// What happened to a task: done, actual duration, assignee, calendar slot. Keyed by the task's stable source key,
/// so generated tasks keep their status when the plan is recalculated.
/// </summary>
public sealed class TaskState : Entity
{
    public required string SourceKey { get; set; }
    public bool Done { get; set; }
    public DateOnly? DoneOn { get; set; }
    public int? ActualMinutes { get; set; }

    /// <summary>Kind and base estimate at completion; together with the actual minutes they feed the estimate learning.</summary>
    public TaskKind? KindAtCompletion { get; set; }
    public int? BaseMinutesAtCompletion { get; set; }

    public Guid? AssigneeId { get; set; }
    public int? EstimateOverride { get; set; }
    /// <summary>Planned start, local wall-clock time of the garden.</summary>
    public DateTime? ScheduledStart { get; set; }

    /// <summary>Title for calendars, in the language of whoever scheduled it (the API itself has no UI text).</summary>
    public string? CalendarTitle { get; set; }

    /// <summary>The Google Calendar event that mirrors this task, and in whose calendar it is.</summary>
    public string? CalendarEventId { get; set; }
    public Guid? CalendarPersonId { get; set; }

    /// <summary>Code of the last calendar sync failure (e.g. "calendar.failed"); null when in sync.</summary>
    public string? CalendarError { get; set; }

    public void Complete(GardenTask task, DateOnly doneOn, int? actualMinutes)
    {
        Done = true;
        DoneOn = doneOn;
        ActualMinutes = actualMinutes;
        KindAtCompletion = task.Kind;
        BaseMinutesAtCompletion = task.BaseMinutes;
    }

    public void Reopen()
    {
        Done = false;
        DoneOn = null;
        ActualMinutes = null;
        KindAtCompletion = null;
        BaseMinutesAtCompletion = null;
    }
}

/// <summary>
/// A task as derived from the plan (not stored). <see cref="Subject"/> is the crop or tree name; for care tasks
/// <see cref="Title"/> holds the action ("Wintersnoei"), for custom tasks the text the user typed.
/// </summary>
public sealed record GardenTask(
    string Key,
    TaskKind Kind,
    DateOnly Date,
    Guid? SiteId,
    Guid? BedId,
    Guid? TreeId,
    Guid? PlantingId,
    string? Subject,
    string? Title,
    int BaseMinutes);

public static class TaskKeys
{
    public static string Planting(Guid plantingId, TaskKind kind, int? year = null) =>
        year is null ? $"planting:{plantingId:N}:{Slug(kind)}" : $"planting:{plantingId:N}:{Slug(kind)}:{year}";

    public static string PlantingCare(Guid plantingId, string action, int year) => $"planting:{plantingId:N}:care:{Normalize(action)}:{year}";
    public static string TreeCare(Guid treeId, string action, int year) => $"tree:{treeId:N}:care:{Normalize(action)}:{year}";
    public static string Manual(Guid id) => $"manual:{id:N}";

    /// <summary>"planting:{id}:…" → id; used to regenerate the tasks of one source.</summary>
    public static (string Source, Guid Id)? Parse(string key)
    {
        var parts = key.Split(':');
        return parts.Length >= 2 && Guid.TryParseExact(parts[1], "N", out var id) ? (parts[0], id) : null;
    }

    private static string Slug(TaskKind kind) => kind.ToString().ToLowerInvariant();
    private static string Normalize(string action) => string.Concat(action.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit));
}
