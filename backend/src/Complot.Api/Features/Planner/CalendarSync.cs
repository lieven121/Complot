using Complot.Api.Data;
using Complot.Domain.Planner;
using Complot.Domain.Work;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Planner;

/// <summary>The garden's time zone: scheduled times are local wall-clock times there; Google needs its IANA id.</summary>
public sealed record GardenTimeZone(TimeZoneInfo Zone, string IanaId)
{
    public static GardenTimeZone From(TimeZoneInfo zone) =>
        new(zone, zone.HasIanaId ? zone.Id : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : "UTC");

    public DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}

/// <summary>Refresh tokens are kept encrypted with ASP.NET Data Protection (keys in the data directory).</summary>
public sealed class CalendarTokens(IDataProtectionProvider protection)
{
    private readonly IDataProtector _protector = protection.CreateProtector("Complot.Calendar.RefreshToken");

    public string Protect(string refreshToken) => _protector.Protect(refreshToken);
    public string Unprotect(CalendarLink link) => _protector.Unprotect(link.ProtectedRefreshToken);
}

/// <summary>
/// Keeps the Google Calendar event of a task in step with its state (scheduled slot, assignee, done). A Google error
/// never blocks the change itself: it is stored on the task state as <see cref="TaskState.CalendarError"/>.
/// </summary>
public sealed class CalendarSync(
    ComplotDbContext db, IGoogleCalendar google, GoogleOptions options, CalendarTokens tokens, GardenTimeZone zone, ILogger<CalendarSync> log)
{
    public async Task SyncAsync(GardenTask task, TaskState state, int minutes, CancellationToken ct)
    {
        if (!options.IsConfigured) return;
        var people = new[] { state.AssigneeId, state.CalendarPersonId }.OfType<Guid>().ToList();
        var links = await db.CalendarLinks.Where(l => people.Contains(l.PersonId)).ToDictionaryAsync(l => l.PersonId, ct);
        var plan = CalendarSyncPlan.For(state, links.ContainsKey);
        state.CalendarError = null;
        try
        {
            if (plan.DeleteFrom is { } from)
            {
                if (links.TryGetValue(from, out var old)) await google.DeleteEventAsync(tokens.Unprotect(old), state.CalendarEventId!, ct);
                state.CalendarEventId = null;
                state.CalendarPersonId = null;
            }
            if (plan.WriteTo is { } to)
            {
                var entry = CalendarEntries.For(task, state, minutes, await LocationAsync(task, ct), null);
                state.CalendarEventId = await google.SaveEventAsync(tokens.Unprotect(links[to]), plan.UpdateExisting ? state.CalendarEventId : null, entry, zone.IanaId, ct);
                state.CalendarPersonId = to;
            }
        }
        catch (GoogleCalendarException e)
        {
            log.LogWarning(e, "Calendar sync of task {Key} failed", task.Key);
            state.CalendarError = e.Code;
        }
    }

    /// <summary>Removes the events of tasks that disappear (deleted manual task or site); best effort.</summary>
    public async Task RemoveAsync(IReadOnlyCollection<TaskState> states, CancellationToken ct)
    {
        if (!options.IsConfigured) return;
        var people = states.Where(s => s.CalendarEventId is not null).Select(s => s.CalendarPersonId).OfType<Guid>().Distinct().ToList();
        var links = await db.CalendarLinks.Where(l => people.Contains(l.PersonId)).ToDictionaryAsync(l => l.PersonId, ct);
        foreach (var state in states.Where(s => s is { CalendarEventId: not null, CalendarPersonId: not null }))
        {
            if (!links.TryGetValue(state.CalendarPersonId!.Value, out var link)) continue;
            try
            {
                await google.DeleteEventAsync(tokens.Unprotect(link), state.CalendarEventId!, ct);
            }
            catch (GoogleCalendarException e)
            {
                log.LogWarning(e, "Removing calendar event of {Key} failed", state.SourceKey);
            }
        }
    }

    /// <summary>"Thuis · K1" or "Thuis · Appelboom": where the work is, for the calendar entry.</summary>
    public async Task<string?> LocationAsync(GardenTask task, CancellationToken ct)
    {
        var site = task.SiteId is { } siteId ? await db.Sites.Where(s => s.Id == siteId).Select(s => s.Name).FirstOrDefaultAsync(ct) : null;
        var place = task.BedId is { } bedId
            ? await db.Beds.Where(b => b.Id == bedId).Select(b => b.Code).FirstOrDefaultAsync(ct)
            : task.TreeId is { } treeId ? await db.Trees.Where(t => t.Id == treeId).Select(t => t.Name).FirstOrDefaultAsync(ct) : null;
        return string.Join(" · ", new[] { site, place }.OfType<string>()) is { Length: > 0 } text ? text : null;
    }
}
