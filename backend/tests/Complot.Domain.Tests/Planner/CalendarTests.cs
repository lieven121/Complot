using Complot.Domain.Planner;
using Complot.Domain.Work;

namespace Complot.Domain.Tests.Planner;

public sealed class CalendarSyncPlanTests
{
    private static readonly Guid Lien = Guid.NewGuid();
    private static readonly Guid Tom = Guid.NewGuid();
    private static readonly DateTime Saturday = new(2026, 10, 24, 9, 0, 0);

    private static TaskState State(Guid? assignee, DateTime? start, string? eventId = null, Guid? eventPerson = null) =>
        new() { SourceKey = "manual:x", AssigneeId = assignee, ScheduledStart = start, CalendarEventId = eventId, CalendarPersonId = eventPerson };

    private static bool Linked(Guid person) => person == Lien || person == Tom;

    [Fact]
    public void Scheduling_for_a_linked_person_writes_a_new_event() =>
        Assert.Equal(new CalendarSyncPlan(null, Lien, false), CalendarSyncPlan.For(State(Lien, Saturday), Linked));

    [Fact]
    public void Moving_in_time_updates_the_existing_event() =>
        Assert.Equal(new CalendarSyncPlan(null, Lien, true), CalendarSyncPlan.For(State(Lien, Saturday, "ev1", Lien), Linked));

    [Fact]
    public void Reassigning_moves_the_event_to_the_other_calendar() =>
        Assert.Equal(new CalendarSyncPlan(Lien, Tom, false), CalendarSyncPlan.For(State(Tom, Saturday, "ev1", Lien), Linked));

    [Fact]
    public void Unscheduling_or_a_person_without_calendar_removes_the_event()
    {
        Assert.Equal(new CalendarSyncPlan(Lien, null, false), CalendarSyncPlan.For(State(Lien, null, "ev1", Lien), Linked));
        Assert.Equal(new CalendarSyncPlan(Lien, null, false), CalendarSyncPlan.For(State(Guid.NewGuid(), Saturday, "ev1", Lien), Linked));
    }

    [Fact]
    public void Nothing_to_do_without_calendars() =>
        Assert.Equal(new CalendarSyncPlan(null, null, false), CalendarSyncPlan.For(State(Guid.NewGuid(), Saturday), Linked));
}

public sealed class CalendarEntryTests
{
    private static readonly GardenTask Task = new("planting:abc:harvest", TaskKind.Harvest, new DateOnly(2026, 10, 24), null, null, null, null, "Spinazie", null, 20);

    [Fact]
    public void Event_uses_the_scheduled_slot_and_the_translated_title()
    {
        var state = new TaskState { SourceKey = Task.Key, ScheduledStart = new DateTime(2026, 10, 24, 9, 30, 0), CalendarTitle = "Oogst Spinazie" };
        var entry = CalendarEntries.For(Task, state, 45, "Thuis · K1", null);
        Assert.Equal("🌱 Oogst Spinazie", entry.Title);
        Assert.Equal(new DateTime(2026, 10, 24, 10, 15, 0), entry.End);

        state.Done = true;
        Assert.StartsWith("✔ ", CalendarEntries.For(Task, state, 45, null, null).Title);
    }
}

public sealed class ICalendarWriterTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    [Fact]
    public void Writes_events_in_utc_with_escaped_text()
    {
        var entry = new CalendarEntry("manual:1", "🌱 Hek herstellen; paal, draad", new DateTime(2026, 7, 4, 9, 0, 0), new DateTime(2026, 7, 4, 10, 0, 0), "Thuis", null);
        var ics = ICalendarWriter.Write("Complot · Lien", [entry], Brussels, new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics);
        Assert.Contains("DTSTART:20260704T070000Z\r\n", ics); // summer time: UTC+2
        Assert.Contains("DTEND:20260704T080000Z\r\n", ics);
        Assert.Contains(@"SUMMARY:🌱 Hek herstellen\; paal\, draad", ics);
        Assert.Contains("UID:manual:1@complot", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
    }

    [Fact]
    public void Folds_long_lines_at_75_octets()
    {
        var entry = new CalendarEntry("x", new string('a', 200), new DateTime(2026, 1, 5, 9, 0, 0), new DateTime(2026, 1, 5, 9, 30, 0), null, null);
        var ics = ICalendarWriter.Write("Complot", [entry], Brussels, DateTime.UtcNow);
        Assert.All(ics.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75, line));
        Assert.Contains("DTSTART:20260105T080000Z", ics); // winter time: UTC+1
    }
}
