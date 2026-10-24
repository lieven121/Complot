using System.Net;
using System.Net.Http.Json;
using System.Web;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Planner;
using Complot.Api.Features.Work;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Planner;

namespace Complot.Api.Tests.Planner;

public sealed class PlannerTests(PlannerApiFactory factory) : IClassFixture<PlannerApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private FakeGoogleCalendar Google => factory.Google;

    private async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;
    }

    private async Task<T> Get<T>(string url) => await Read<T>(await _client.GetAsync(url, Ct));

    private async Task<PersonDto> Person(string name)
    {
        var existing = (await Get<PersonDto[]>("/api/people")).FirstOrDefault(p => p.Name == name);
        return existing ?? await Read<PersonDto>(await _client.PostAsJsonAsync("/api/people", new PersonInput(name, "#3f7d4e"), ApiJson.Options, Ct));
    }

    /// <summary>Goes through the consent flow like a browser; the fake Google hands out "{code}@gmail.com".</summary>
    private async Task Connect(PersonDto person, string code)
    {
        var start = await _client.GetAsync($"/api/planner/google/connect?personId={person.Id}&returnUrl=%2Fplanner%3Fwho%3D{person.Id}", Ct);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var state = HttpUtility.ParseQueryString(start.Headers.Location!.Query)["state"];
        var back = await _client.GetAsync($"/api/planner/google/callback?code={code}&state={Uri.EscapeDataString(state!)}", Ct);
        Assert.Equal($"/planner?who={person.Id}&calendar=connected", back.Headers.Location?.OriginalString);
    }

    private async Task<TaskDto> NewTask(string title, PersonDto? assignee, DateOnly due)
    {
        var home = (await Get<SiteDto[]>("/api/sites")).First(s => s.Name == "Thuis");
        return await Read<TaskDto>(await _client.PostAsJsonAsync("/api/manual-tasks",
            new ManualTaskInput(home.Id, null, title, due, 60, assignee?.Id), ApiJson.Options, Ct));
    }

    private async Task<TaskDto> Schedule(TaskDto task, PersonDto? who, DateTime? start, string? title = null) =>
        await Read<TaskDto>(await _client.PutAsJsonAsync($"/api/tasks/{Uri.EscapeDataString(task.Key)}/state",
            new TaskStateInput(who?.Id, null, start, title), ApiJson.Options, Ct));

    private FakeGoogleCalendar.Event EventOf(string key) => Google.Events.Values.Single(e => e.Entry.Uid == key);

    [Fact]
    public async Task Settings_report_google_and_the_garden_time_zone()
    {
        var settings = await Get<PlannerSettingsDto>("/api/planner/settings");
        Assert.True(settings.GoogleEnabled);
        Assert.False(string.IsNullOrEmpty(settings.TimeZone));
    }

    [Fact]
    public async Task Scheduled_task_follows_the_assignee_calendar()
    {
        var anna = await Person("Anna");
        var bert = await Person("Bert");
        var carl = await Person("Carl");
        await Connect(anna, "anna");
        await Connect(bert, "bert");
        Assert.Equal("anna@gmail.com", (await Get<PersonCalendarDto[]>("/api/planner/calendars")).Single(c => c.PersonId == anna.Id).GoogleAccount);

        var task = await NewTask("Hek herstellen", anna, new DateOnly(2026, 11, 20));
        var slot = new DateTime(2026, 11, 14, 9, 0, 0);
        var scheduled = await Schedule(task, anna, slot, "Hek herstellen");
        Assert.True(scheduled.CalendarSynced);
        var ev = EventOf(task.Key);
        Assert.Equal(("refresh-anna", "🌱 Hek herstellen", slot, slot.AddHours(1), "Thuis"), (ev.RefreshToken, ev.Entry.Title, ev.Entry.Start, ev.Entry.End, ev.Entry.Location));

        // Moving in time updates the same event; another person moves it to their calendar.
        await Schedule(task, anna, slot.AddHours(2));
        Assert.Equal(slot.AddHours(2), EventOf(task.Key).Entry.Start);
        await Schedule(task, bert, slot.AddHours(2));
        Assert.Equal("refresh-bert", EventOf(task.Key).RefreshToken);

        await Read<TaskDto>(await _client.PostAsJsonAsync($"/api/tasks/{Uri.EscapeDataString(task.Key)}/complete", new CompleteTaskInput(50, null), ApiJson.Options, Ct));
        Assert.StartsWith("✔ ", EventOf(task.Key).Entry.Title);

        // Someone without a linked calendar, or unscheduling, removes the event.
        Assert.False((await Schedule(task, carl, slot)).CalendarSynced);
        Assert.DoesNotContain(Google.Events.Values, e => e.Entry.Uid == task.Key);
        await Schedule(task, bert, slot);
        await Schedule(task, bert, null);
        Assert.DoesNotContain(Google.Events.Values, e => e.Entry.Uid == task.Key);
    }

    [Fact]
    public async Task Google_failure_keeps_the_change_and_reports_it()
    {
        var dirk = await Person("Dirk");
        await Connect(dirk, "dirk");
        var task = await NewTask("Snoeien", dirk, new DateOnly(2026, 11, 20));
        Google.Failing = true;
        try
        {
            var result = await Schedule(task, dirk, new DateTime(2026, 11, 16, 10, 0, 0));
            Assert.Equal(new DateTime(2026, 11, 16, 10, 0, 0), result.ScheduledStart);
            Assert.Equal("calendar.failed", result.CalendarError);
            Assert.False(result.CalendarSynced);
            Assert.Equal(HttpStatusCode.BadGateway, (await _client.GetAsync($"/api/planner/busy?personId={dirk.Id}&from=2026-11-16&to=2026-11-22", Ct)).StatusCode);
        }
        finally
        {
            Google.Failing = false;
        }
        Assert.Null((await Schedule(task, dirk, new DateTime(2026, 11, 16, 10, 0, 0))).CalendarError);
    }

    [Fact]
    public async Task Busy_times_come_back_in_garden_time()
    {
        var eva = await Person("Eva");
        await Connect(eva, "eva");
        var zone = TimeZoneInfo.Local;
        var start = new DateTime(2026, 11, 17, 14, 0, 0);
        Google.Busy.Add(new BusyInterval(TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeZoneInfo.ConvertTimeToUtc(start.AddHours(1), zone)));
        var busy = await Get<BusyDto[]>($"/api/planner/busy?personId={eva.Id}&from=2026-11-16&to=2026-11-22");
        Assert.Equal([new BusyDto(start, start.AddHours(1))], busy);
        Assert.Empty(await Get<BusyDto[]>($"/api/planner/busy?personId={(await Person("Zonder agenda")).Id}&from=2026-11-16&to=2026-11-22"));
    }

    [Fact]
    public async Task Ical_feed_lists_the_persons_scheduled_tasks()
    {
        var fien = await Person("Fien");
        var task = await NewTask("Compost omzetten", fien, DateOnly.FromDateTime(DateTime.Today).AddDays(10));
        await Schedule(task, fien, DateTime.Today.AddDays(3).AddHours(9), "Compost omzetten");

        var feed = await Read<FeedDto>(await _client.PostAsync($"/api/planner/people/{fien.Id}/feed", null, Ct));
        var anonymous = factory.CreateClient();
        var ics = await anonymous.GetStringAsync($"/ical/{feed.Token}.ics", Ct);
        Assert.Contains("SUMMARY:🌱 Compost omzetten", ics);
        Assert.Contains("X-WR-CALNAME:Complot · Fien", ics);

        await Read<FeedDto>(await _client.PostAsync($"/api/planner/people/{fien.Id}/feed", null, Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/ical/{feed.Token}.ics", Ct)).StatusCode);
    }

    [Fact]
    public async Task Task_planned_far_from_its_due_date_shows_in_the_planned_week()
    {
        var task = await NewTask("Zaaigoed bestellen", null, new DateOnly(2027, 3, 1));
        await Schedule(task, null, new DateTime(2026, 12, 2, 10, 0, 0));
        var week = await Get<TaskDto[]>("/api/tasks?from=2026-11-30&to=2026-12-06");
        Assert.Contains(week, t => t.Key == task.Key);
    }
}
