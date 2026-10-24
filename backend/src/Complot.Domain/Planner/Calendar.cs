using System.Globalization;
using System.Text;
using Complot.Domain.Common;
using Complot.Domain.Work;

namespace Complot.Domain.Planner;

/// <summary>A person's Google Calendar, linked with OAuth. The refresh token is stored encrypted.</summary>
public sealed class CalendarLink : Entity
{
    public Guid PersonId { get; set; }
    public required string AccountEmail { get; set; }
    public required string ProtectedRefreshToken { get; set; }
}

/// <summary>What a scheduled task looks like in a calendar.</summary>
public sealed record CalendarEntry(string Uid, string Title, DateTime Start, DateTime End, string? Location, string? Description);

/// <summary>
/// Which calendar writes keep a task's event in step with its state: remove it from the calendar it is in when the task
/// is unscheduled or moves to someone else, and write it to the assignee's calendar when they have one linked.
/// </summary>
public sealed record CalendarSyncPlan(Guid? DeleteFrom, Guid? WriteTo, bool UpdateExisting)
{
    public static CalendarSyncPlan For(TaskState state, Func<Guid, bool> hasCalendar)
    {
        Guid? target = state is { ScheduledStart: not null, AssigneeId: { } person } && hasCalendar(person) ? person : null;
        Guid? current = state.CalendarEventId is null ? null : state.CalendarPersonId;
        return current == target
            ? new CalendarSyncPlan(null, target, UpdateExisting: target is not null)
            : new CalendarSyncPlan(current, target, UpdateExisting: false);
    }
}

public static class CalendarEntries
{
    /// <summary>The event for a scheduled task; done tasks keep their slot but get a check mark.</summary>
    public static CalendarEntry For(GardenTask task, TaskState state, int minutes, string? location, string? description)
    {
        var start = state.ScheduledStart ?? throw new InvalidOperationException("Task is not scheduled.");
        var title = state.CalendarTitle ?? task.Title ?? task.Subject ?? task.Kind.ToString();
        return new CalendarEntry(task.Key, $"{(state.Done ? "✔ " : "")}🌱 {title}", start, start.AddMinutes(minutes), location, description);
    }
}

/// <summary>RFC 5545 calendar text for a read-only feed; times are written in UTC.</summary>
public static class ICalendarWriter
{
    public static string Write(string name, IEnumerable<CalendarEntry> entries, TimeZoneInfo zone, DateTime utcNow)
    {
        var text = new StringBuilder();
        void Line(string value) => Fold(text, value);
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Complot//Tuinplanner//NL");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line($"X-WR-CALNAME:{Escape(name)}");
        foreach (var e in entries)
        {
            Line("BEGIN:VEVENT");
            Line($"UID:{Escape(e.Uid)}@complot");
            Line($"DTSTAMP:{Utc(utcNow)}");
            Line($"DTSTART:{Utc(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(e.Start, DateTimeKind.Unspecified), zone))}");
            Line($"DTEND:{Utc(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(e.End, DateTimeKind.Unspecified), zone))}");
            Line($"SUMMARY:{Escape(e.Title)}");
            if (e.Location is not null) Line($"LOCATION:{Escape(e.Location)}");
            if (e.Description is not null) Line($"DESCRIPTION:{Escape(e.Description)}");
            Line("TRANSP:TRANSPARENT");
            Line("END:VEVENT");
        }
        Line("END:VCALENDAR");
        return text.ToString();
    }

    private static string Utc(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace(";", @"\;", StringComparison.Ordinal)
            .Replace(",", @"\,", StringComparison.Ordinal).Replace("\r\n", @"\n", StringComparison.Ordinal).Replace("\n", @"\n", StringComparison.Ordinal);

    /// <summary>Lines longer than 75 octets continue on the next line after a space (RFC 5545 §3.1).</summary>
    private static void Fold(StringBuilder text, string line)
    {
        var bytes = 0;
        var element = StringInfo.GetTextElementEnumerator(line);
        while (element.MoveNext())
        {
            var part = element.GetTextElement();
            var size = Encoding.UTF8.GetByteCount(part);
            if (bytes + size > 75)
            {
                text.Append("\r\n ");
                bytes = 1;
            }
            text.Append(part);
            bytes += size;
        }
        text.Append("\r\n");
    }
}
