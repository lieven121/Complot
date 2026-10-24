using System.Collections.Concurrent;
using Complot.Api.Features.Planner;
using Complot.Domain.Planner;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Complot.Api.Tests.Infrastructure;

/// <summary>In-memory Google Calendar: records the events Complot writes, per refresh token.</summary>
public sealed class FakeGoogleCalendar : IGoogleCalendar
{
    public sealed record Event(string RefreshToken, CalendarEntry Entry, string TimeZone);

    public ConcurrentDictionary<string, Event> Events { get; } = new();
    public List<BusyInterval> Busy { get; } = [];

    /// <summary>When set, every call fails like an unreachable Google.</summary>
    public bool Failing { get; set; }

    public Uri AuthorizeUrl(string state, Uri redirectUri) =>
        new($"https://accounts.example/auth?state={Uri.EscapeDataString(state)}&redirect_uri={Uri.EscapeDataString(redirectUri.ToString())}");

    public Task<GoogleAccount> ExchangeCodeAsync(string code, Uri redirectUri, CancellationToken ct) =>
        Task.FromResult(new GoogleAccount($"refresh-{code}", $"{code}@gmail.com"));

    public Task<IReadOnlyList<BusyInterval>> BusyAsync(string refreshToken, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        Fail();
        return Task.FromResult<IReadOnlyList<BusyInterval>>(Busy.Where(b => b.End > fromUtc && b.Start < toUtc).ToList());
    }

    public Task<string> SaveEventAsync(string refreshToken, string? eventId, CalendarEntry entry, string timeZone, CancellationToken ct)
    {
        Fail();
        var id = eventId is not null && Events.ContainsKey(eventId) ? eventId : Guid.NewGuid().ToString("N");
        Events[id] = new Event(refreshToken, entry, timeZone);
        return Task.FromResult(id);
    }

    public Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken ct)
    {
        Fail();
        Events.TryRemove(eventId, out _);
        return Task.CompletedTask;
    }

    public Task RevokeAsync(string refreshToken, CancellationToken ct) => Task.CompletedTask;

    private void Fail()
    {
        if (Failing) throw new GoogleCalendarException("calendar.failed", "Google is down (fake).");
    }
}

/// <summary>Demo garden in open mode with a Google OAuth client configured, talking to <see cref="Google"/>.</summary>
public sealed class PlannerApiFactory : ApiFactory
{
    public FakeGoogleCalendar Google { get; } = new();

    protected override IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>(base.Settings)
    {
        ["Seed:DemoData"] = "true",
        ["Google:ClientId"] = "test-client",
        ["Google:ClientSecret"] = "test-secret",
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IGoogleCalendar>(Google)));
    }
}
