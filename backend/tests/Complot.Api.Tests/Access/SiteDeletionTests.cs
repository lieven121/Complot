using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Complot.Api.Data;
using Complot.Api.Features.Admin;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Work;
using Complot.Api.Hosting;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Access;
using Complot.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Complot.Api.Tests.Access;

/// <summary>Deletes the demo allotment, so it gets its own API instance.</summary>
public sealed class SiteDeletionTests(SignedInApiFactory factory) : IClassFixture<SignedInApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;
    }

    private static async Task<string> Code(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options, Ct)).GetProperty("code").GetString()!;
    }

    [Fact]
    public async Task Archived_site_is_deleted_with_everything_on_it_but_the_estimate_history()
    {
        var admin = factory.Admin();
        var site = (await Read<SiteDto[]>(await admin.GetAsync("/api/sites", Ct))).Single(s => s.Name == "Volkstuin");
        await Read<UserDto>(await admin.PostAsJsonAsync("/api/admin/users",
            new UserInput("lid@example.com", null, false, null, [new SiteRoleDto(site.Id, SiteRole.Edit)]), ApiJson.Options, Ct));

        // Something to clean up: a note with a photo, a completed task (history) and an open task with an assignee.
        var note = await Read<NoteDto>(await admin.PostAsJsonAsync("/api/notes",
            new NoteInput(site.Id, null, null, null, null, new DateOnly(2026, 6, 1), NoteTag.Observation, "Slakken", null, null), ApiJson.Options, Ct));
        using (var photo = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]))
        using (var form = new MultipartFormDataContent { { photo, "file", "slak.jpg" } })
        {
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsync($"/api/notes/{note.Id}/photo", form, Ct)).StatusCode);
        }
        var tasks = await Read<TaskDto[]>(await admin.GetAsync($"/api/tasks?siteId={site.Id}&from=2026-01-01&to=2026-12-31", Ct));
        var notDone = tasks.Where(t => !t.Done).ToList();
        var (done, open) = (notDone[0], notDone[1]);
        await Read<TaskDto>(await admin.PostAsJsonAsync($"/api/tasks/{Uri.EscapeDataString(done.Key)}/complete", new CompleteTaskInput(25, null), ApiJson.Options, Ct));
        await Read<TaskDto>(await admin.PutAsJsonAsync($"/api/tasks/{Uri.EscapeDataString(open.Key)}/state", new TaskStateInput(null, 50, null), ApiJson.Options, Ct));

        Assert.Equal("site.notArchived", await Code(await admin.DeleteAsync($"/api/sites/{site.Id}?confirmName=Volkstuin", Ct)));
        await Read<SiteDto>(await admin.PostAsync($"/api/sites/{site.Id}/archive", null, Ct));
        Assert.Equal("site.confirmName", await Code(await admin.DeleteAsync($"/api/sites/{site.Id}?confirmName=volkstuin", Ct)));
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/sites/{site.Id}?confirmName=Volkstuin", Ct)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ComplotDbContext>();
        Assert.False(await db.Sites.AnyAsync(s => s.Id == site.Id, Ct));
        Assert.False(await db.Beds.AnyAsync(b => b.SiteId == site.Id, Ct));
        Assert.False(await db.Plantings.AnyAsync(p => p.Bed.SiteId == site.Id, Ct));
        Assert.False(await db.Notes.AnyAsync(n => n.SiteId == site.Id, Ct));
        Assert.False(await db.SiteAccess.AnyAsync(a => a.SiteId == site.Id, Ct));
        Assert.True(await db.TaskStates.AnyAsync(s => s.SourceKey == done.Key && s.Done, Ct));
        Assert.False(await db.TaskStates.AnyAsync(s => s.SourceKey == open.Key, Ct));
        var photoDir = scope.ServiceProvider.GetRequiredService<IOptions<StorageOptions>>().Value.PhotoDir;
        Assert.Empty(Directory.Exists(photoDir) ? Directory.GetFiles(photoDir, $"{note.Id:N}.*") : []);

        // The other site is untouched, and the member who only had this site waits for access again.
        Assert.Contains(await Read<SiteDto[]>(await admin.GetAsync("/api/sites", Ct)), s => s.Name == "Thuis");
        Assert.True((await Read<UserDto[]>(await admin.GetAsync("/api/admin/users", Ct))).Single(u => u.Email == "lid@example.com").IsPending);
    }
}
