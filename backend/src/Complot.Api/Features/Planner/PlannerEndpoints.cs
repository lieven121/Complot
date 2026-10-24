using System.Security.Cryptography;
using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Api.Features.Work;
using Complot.Domain.Access;
using Complot.Domain.Planner;
using Complot.Domain.Work;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Planner;

/// <summary><see cref="GoogleEnabled"/>: a Google OAuth client is configured, so people can link their calendar.</summary>
public sealed record PlannerSettingsDto(bool GoogleEnabled, string TimeZone);

/// <summary>
/// A person's calendars: the linked Google account (if any) and the secret iCal feed token. The token and the
/// management actions are only there for whoever may manage that person's calendar (<see cref="CanManage"/>).
/// </summary>
public sealed record PersonCalendarDto(Guid PersonId, string? GoogleAccount, string? FeedToken, bool CanManage);

/// <summary>A busy block in someone's Google Calendar, local garden time.</summary>
public sealed record BusyDto(DateTime Start, DateTime End);

public sealed record FeedDto(string Token);

public static class PlannerEndpoints
{
    /// <summary>The iCal feed shows scheduled tasks from a month back to half a year ahead.</summary>
    private const int FeedDaysBack = 30;
    private const int FeedDaysAhead = 183;

    public static IServiceCollection AddPlanner(this IServiceCollection services, IConfiguration configuration)
    {
        var google = configuration.GetSection(GoogleOptions.Section).Get<GoogleOptions>() ?? new GoogleOptions();
        services.AddSingleton(google);
        services.AddSingleton(sp => GardenTimeZone.From(sp.GetRequiredService<TimeProvider>().LocalTimeZone));
        services.AddMemoryCache();
        services.AddHttpClient<IGoogleCalendar, GoogleCalendarClient>();
        services.AddSingleton<CalendarTokens>();
        services.AddScoped<CalendarSync>();
        return services;
    }

    public static void MapPlanner(this IEndpointRouteBuilder api)
    {
        var planner = api.MapGroup("/planner").WithTags("Planner");
        planner.MapGet("/settings", (GoogleOptions google, GardenTimeZone zone) => new PlannerSettingsDto(google.IsConfigured, zone.IanaId))
            .WithName("GetPlannerSettings");
        planner.MapGet("/calendars", Calendars).WithName("ListPersonCalendars");
        planner.MapGet("/busy", Busy).WithName("GetBusyTimes");
        planner.MapGet("/google/connect", Connect).WithName("ConnectGoogleCalendar");
        planner.MapGet("/google/callback", Callback).ExcludeFromDescription();
        planner.MapDelete("/people/{personId:guid}/google", Disconnect).WithName("DisconnectGoogleCalendar");
        planner.MapPost("/people/{personId:guid}/feed", NewFeed).WithName("RenewCalendarFeed");
        planner.MapDelete("/people/{personId:guid}/feed", DeleteFeed).WithName("DeleteCalendarFeed");
    }

    /// <summary><c>/ical/{token}.ics</c>: read-only subscription, anonymous (the token is the secret).</summary>
    public static void MapCalendarFeed(this IEndpointRouteBuilder app) =>
        app.MapGet("/ical/{token}.ics", Feed).AllowAnonymous().ExcludeFromDescription();

    private static async Task<PersonCalendarDto[]> Calendars(ComplotDbContext db, CurrentUser current, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var links = await db.CalendarLinks.AsNoTracking().ToDictionaryAsync(l => l.PersonId, l => l.AccountEmail, ct);
        return (await db.People.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct))
            .Select(p => new PersonCalendarDto(p.Id, links.GetValueOrDefault(p.Id), user.ManagesCalendarOf(p.Id) ? p.CalendarFeedToken : null, user.ManagesCalendarOf(p.Id)))
            .ToArray();
    }

    /// <summary>Busy times only (no event details), so everyone planning can see when a person is free.</summary>
    private static async Task<Results<Ok<BusyDto[]>, ProblemHttpResult>> Busy(
        Guid personId, DateOnly from, DateOnly to, ComplotDbContext db, IGoogleCalendar google, GoogleOptions options,
        CalendarTokens tokens, GardenTimeZone zone, CancellationToken ct)
    {
        if (!options.IsConfigured || await db.CalendarLinks.AsNoTracking().FirstOrDefaultAsync(l => l.PersonId == personId, ct) is not { } link)
            return TypedResults.Ok(Array.Empty<BusyDto>());
        try
        {
            var busy = await google.BusyAsync(tokens.Unprotect(link), zone.ToUtc(from.ToDateTime(TimeOnly.MinValue)), zone.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue)), ct);
            return TypedResults.Ok(busy.Select(b => new BusyDto(zone.ToLocal(b.Start), zone.ToLocal(b.End))).ToArray());
        }
        catch (GoogleCalendarException e)
        {
            return ApiProblems.BadGateway(e.Code);
        }
    }

    private static IDataProtector StateProtector(IDataProtectionProvider protection) => protection.CreateProtector("Complot.Calendar.OAuthState");

    /// <summary>Who started the link flow, so the callback cannot be completed in someone else's session.</summary>
    private static string Caller(UserAccess user) => user.UserId?.ToString() ?? user.LinkId?.ToString() ?? "open";

    private static Uri RedirectUri(HttpRequest request, GoogleOptions options) =>
        new(options.RedirectUri ?? $"{request.Scheme}://{request.Host}{request.PathBase}/api/planner/google/callback");

    /// <summary>Starts the Google consent screen for a person's own calendar; comes back to <paramref name="returnUrl"/>.</summary>
    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> Connect(
        Guid personId, string? returnUrl, ComplotDbContext db, CurrentUser current, GoogleOptions options, IGoogleCalendar google,
        IDataProtectionProvider protection, HttpRequest request, CancellationToken ct)
    {
        if (!options.IsConfigured) return ApiProblems.Conflict("calendar.notConfigured");
        if (!await db.People.AnyAsync(p => p.Id == personId, ct)) return ApiProblems.NotFound();
        var user = await current.GetAsync(ct);
        if (!user.ManagesCalendarOf(personId)) return ApiProblems.Forbidden("calendar.notYours");

        var target = returnUrl is { } url && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) ? url : "/planner";
        var state = StateProtector(protection).ToTimeLimitedDataProtector()
            .Protect($"{personId:N}|{Caller(user)}|{target}", TimeSpan.FromMinutes(15));
        return TypedResults.Redirect(google.AuthorizeUrl(state, RedirectUri(request, options)).ToString());
    }

    private static async Task<RedirectHttpResult> Callback(
        string? code, string? state, string? error, ComplotDbContext db, CurrentUser current, GoogleOptions options, IGoogleCalendar google,
        CalendarTokens tokens, IDataProtectionProvider protection, HttpRequest request, ILoggerFactory logs, CancellationToken ct)
    {
        string[] parts;
        try
        {
            parts = StateProtector(protection).ToTimeLimitedDataProtector().Unprotect(state ?? "").Split('|', 3);
        }
        catch (CryptographicException)
        {
            return TypedResults.Redirect("/planner?calendar=failed");
        }
        var (personId, caller, target) = (Guid.ParseExact(parts[0], "N"), parts[1], parts[2]);
        var back = (string result) => TypedResults.Redirect($"{target}{(target.Contains('?') ? '&' : '?')}calendar={result}");
        var user = await current.GetAsync(ct);
        if (Caller(user) != caller || !user.ManagesCalendarOf(personId)) return back("failed");
        if (error is not null || code is null) return back("denied");

        try
        {
            var account = await google.ExchangeCodeAsync(code, RedirectUri(request, options), ct);
            var link = await db.CalendarLinks.FirstOrDefaultAsync(l => l.PersonId == personId, ct);
            if (link is null) db.CalendarLinks.Add(link = new CalendarLink { PersonId = personId, AccountEmail = "", ProtectedRefreshToken = "" });
            link.AccountEmail = account.Email;
            link.ProtectedRefreshToken = tokens.Protect(account.RefreshToken);
            await db.SaveChangesAsync(ct);
            return back("connected");
        }
        catch (GoogleCalendarException e)
        {
            logs.CreateLogger(nameof(PlannerEndpoints)).LogWarning(e, "Linking Google Calendar failed");
            return back("failed");
        }
    }

    /// <summary>Unlinks the calendar and revokes Complot's access; existing events stay in the person's calendar.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> Disconnect(
        Guid personId, ComplotDbContext db, CurrentUser current, IGoogleCalendar google, CalendarTokens tokens, CancellationToken ct)
    {
        if (!(await current.GetAsync(ct)).ManagesCalendarOf(personId)) return ApiProblems.Forbidden("calendar.notYours");
        if (await db.CalendarLinks.FirstOrDefaultAsync(l => l.PersonId == personId, ct) is not { } link) return ApiProblems.NotFound();
        try
        {
            await google.RevokeAsync(tokens.Unprotect(link), ct);
        }
        catch (Exception e) when (e is GoogleCalendarException or HttpRequestException or CryptographicException)
        {
            // Unlinking must work even when Google is unreachable or the token is already invalid.
        }
        db.CalendarLinks.Remove(link);
        await db.TaskStates.Where(s => s.CalendarPersonId == personId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CalendarEventId, (string?)null).SetProperty(x => x.CalendarPersonId, (Guid?)null), ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>A new secret feed address; the old one stops working.</summary>
    private static async Task<Results<Ok<FeedDto>, ProblemHttpResult>> NewFeed(Guid personId, ComplotDbContext db, CurrentUser current, CancellationToken ct)
    {
        if (!(await current.GetAsync(ct)).ManagesCalendarOf(personId)) return ApiProblems.Forbidden("calendar.notYours");
        if (await db.People.FindAsync([personId], ct) is not { } person) return ApiProblems.NotFound();
        // Feed tokens are shown again in the app (people subscribe once), so they are stored as they are.
        person.CalendarFeedToken = AccessTokens.Create().Token;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new FeedDto(person.CalendarFeedToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteFeed(Guid personId, ComplotDbContext db, CurrentUser current, CancellationToken ct)
    {
        if (!(await current.GetAsync(ct)).ManagesCalendarOf(personId)) return ApiProblems.Forbidden("calendar.notYours");
        if (await db.People.FindAsync([personId], ct) is not { } person) return ApiProblems.NotFound();
        person.CalendarFeedToken = null;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Feed(
        string token, ComplotDbContext db, TaskService tasks, CalendarSync sync, GardenTimeZone zone, TimeProvider time, CancellationToken ct)
    {
        if (await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.CalendarFeedToken == token, ct) is not { } person) return TypedResults.NotFound();
        var now = zone.ToLocal(time.GetUtcNow().UtcDateTime);
        var (from, to) = (now.Date.AddDays(-FeedDaysBack), now.Date.AddDays(FeedDaysAhead));
        var states = await db.TaskStates.AsNoTracking()
            .Where(s => s.AssigneeId == person.Id && s.ScheduledStart >= from && s.ScheduledStart < to)
            .ToListAsync(ct);
        var factors = await tasks.FactorsAsync(ct);
        var entries = new List<CalendarEntry>();
        foreach (var state in states)
        {
            if (await tasks.FindAsync(state.SourceKey, ct) is not { } task) continue;
            entries.Add(CalendarEntries.For(task, state, TaskEstimates.Estimate(task, state, factors), await sync.LocationAsync(task, ct), null));
        }
        var ics = ICalendarWriter.Write($"Complot · {person.Name}", entries.OrderBy(e => e.Start), zone.Zone, time.GetUtcNow().UtcDateTime);
        return TypedResults.Text(ics, "text/calendar; charset=utf-8");
    }
}
