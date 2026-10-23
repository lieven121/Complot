using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Work;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Work;

namespace Complot.Api.Tests.Work;

public sealed class WorkTests(SeededApiFactory factory) : IClassFixture<SeededApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<T?> Get<T>(string url) => _client.GetFromJsonAsync<T>(url, ApiJson.Options, Ct);
    private Task<HttpResponseMessage> Post<T>(string url, T body) => _client.PostAsJsonAsync(url, body, ApiJson.Options, Ct);
    private Task<HttpResponseMessage> Put<T>(string url, T body) => _client.PutAsJsonAsync(url, body, ApiJson.Options, Ct);
    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;
    }

    private static string Url(string key) => Uri.EscapeDataString(key);
    private async Task<SiteDto> Home() => (await Get<SiteDto[]>("/api/sites"))!.First(s => s.Name == "Thuis");

    [Fact]
    public async Task Task_list_combines_plan_tree_care_and_manual_tasks()
    {
        var tasks = await Get<TaskDto[]>("/api/tasks?from=2026-01-01&to=2026-12-31");
        Assert.Contains(tasks!, t => t.Kind == TaskKind.Harvest && t.Subject == "Boerenkool");
        Assert.Contains(tasks!, t => t.Kind == TaskKind.Care && t.Title == "Wintersnoei" && t.Subject == "Appelboom");
        Assert.Contains(tasks!, t => t.Kind == TaskKind.Custom && t.Title!.StartsWith("Compost omzetten", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sample_history_teaches_the_estimates()
    {
        var factors = await Get<EstimateFactorDto[]>("/api/tasks/estimates");
        var clear = factors!.Single(f => f.Kind == TaskKind.Clear);
        Assert.True(clear.Samples >= 2);
        Assert.InRange(clear.Factor, 1.2, 1.5);
    }

    [Fact]
    public async Task Completing_a_task_records_the_actual_time_and_can_be_undone()
    {
        var task = (await Get<TaskDto[]>("/api/tasks"))!.First(t => !t.Done && t.Kind != TaskKind.Custom);

        var done = await Read<TaskDto>(await Post($"/api/tasks/{Url(task.Key)}/complete", new CompleteTaskInput(45, null)));
        Assert.True(done.Done);
        Assert.Equal(45, done.ActualMinutes);

        var reopened = await Read<TaskDto>(await Post($"/api/tasks/{Url(task.Key)}/reopen", new { }));
        Assert.False(reopened.Done);
        Assert.Null(reopened.ActualMinutes);
    }

    [Fact]
    public async Task Task_can_be_assigned_estimated_and_scheduled()
    {
        var person = (await Get<PersonDto[]>("/api/people"))!.First();
        var task = (await Get<TaskDto[]>("/api/tasks"))!.First(t => !t.Done);
        var slot = new DateTime(2026, 10, 24, 10, 0, 0);

        var updated = await Read<TaskDto>(await Put($"/api/tasks/{Url(task.Key)}/state", new TaskStateInput(person.Id, 25, slot)));
        Assert.Equal((person.Id, 25, slot), (updated.AssigneeId!.Value, updated.EstimateMinutes, updated.ScheduledStart!.Value));

        Assert.Equal(HttpStatusCode.NotFound, (await Put("/api/tasks/planting:nope/state", new TaskStateInput(null, null, null))).StatusCode);
    }

    [Fact]
    public async Task Manual_tasks_can_be_added_and_removed()
    {
        var site = await Home();
        var created = await Read<TaskDto>(await Post("/api/manual-tasks", new ManualTaskInput(site.Id, null, "Haag snoeien", new DateOnly(2026, 11, 2), 60, null)));
        Assert.Equal(TaskKind.Custom, created.Kind);
        Assert.Contains(await Get<TaskDto[]>("/api/tasks?from=2026-11-01&to=2026-11-30") ?? [], t => t.Key == created.Key);

        var id = created.Key.Split(':')[1];
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/manual-tasks/{Guid.ParseExact(id, "N")}", Ct)).StatusCode);
        Assert.DoesNotContain(await Get<TaskDto[]>("/api/tasks?from=2026-11-01&to=2026-11-30") ?? [], t => t.Key == created.Key);
    }

    [Fact]
    public async Task Notes_with_harvest_and_photo()
    {
        var site = await Home();
        var bed = (await Get<SiteLayoutDto>($"/api/sites/{site.Id}/layout"))!.Beds.Single(b => b.Code == "A2");

        var note = await Read<NoteDto>(await Post("/api/notes",
            new NoteInput(site.Id, bed.Id, null, null, null, new DateOnly(2026, 10, 22), NoteTag.Harvest, "Boerenkool geplukt", 1.5m, "kg")));
        Assert.Equal(1.5m, note.HarvestQuantity);

        using var photo = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent { { photo, "file", "kool.jpg" } };
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsync($"/api/notes/{note.Id}/photo", form, Ct)).StatusCode);

        var photoResponse = await _client.GetAsync($"/api/notes/{note.Id}/photo", Ct);
        Assert.Equal("image/jpeg", photoResponse.Content.Headers.ContentType?.MediaType);

        var notes = await Get<NoteDto[]>($"/api/notes?bedId={bed.Id}");
        Assert.Contains(notes!, n => n.Id == note.Id && n.HasPhoto);
    }

    [Fact]
    public async Task Harvest_note_needs_a_unit()
    {
        var site = await Home();
        var response = await Post("/api/notes", new NoteInput(site.Id, null, null, null, null, new DateOnly(2026, 10, 22), NoteTag.Harvest, "x", 2, null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_notes_are_linked_to_their_crop()
    {
        var notes = await Get<NoteDto[]>($"/api/notes?siteId={(await Home()).Id}");
        Assert.Contains(notes!, n => n.Tag == NoteTag.Disease && n.VarietyId is not null);
        Assert.Contains(notes!, n => n.Tag == NoteTag.Harvest && n.TreeId is not null && n.HarvestQuantity == 40);
    }
}
