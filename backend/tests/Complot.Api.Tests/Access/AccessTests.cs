using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Complot.Api.Auth;
using Complot.Api.Features.Admin;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Work;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Access;
using Complot.Domain.Work;

namespace Complot.Api.Tests.Access;

public sealed class AccessTests(SignedInApiFactory factory) : IClassFixture<SignedInApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;
    }

    private static async Task<T> Get<T>(HttpClient client, string url) => await Read<T>(await client.GetAsync(url, Ct));

    private static Task<HttpResponseMessage> Post<T>(HttpClient client, string url, T body) => client.PostAsJsonAsync(url, body, ApiJson.Options, Ct);

    private static async Task<string> Code(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(ApiJson.Options, Ct)).GetProperty("code").GetString()!;
    }

    private async Task<SiteDto> Site(string name) => (await Get<SiteDto[]>(factory.Admin(), "/api/sites")).Single(s => s.Name == name);

    /// <summary>Invites <paramref name="email"/> with the given site roles, then returns a client signed in as that person.</summary>
    private async Task<HttpClient> Member(string email, params SiteRoleDto[] sites)
    {
        await Read<UserDto>(await Post(factory.Admin(), "/api/admin/users", new UserInput(email, null, false, null, [.. sites])));
        return factory.ClientFor($"sub-{email}", email);
    }

    private static NoteInput Note(Guid siteId) =>
        new(siteId, null, null, null, null, new DateOnly(2026, 6, 1), NoteTag.Observation, "Bladluis", null, null);

    [Fact]
    public async Task Unknown_login_waits_for_access()
    {
        var client = factory.ClientFor("stranger", "stranger@example.com", "Vreemde");

        var me = await Get<MeDto>(client, "/api/me");
        Assert.True(me.IsPending);
        Assert.Equal("stranger@example.com", me.Email);
        Assert.Equal("access.pending", await Code(await client.GetAsync("/api/families", Ct), HttpStatusCode.Forbidden));

        var users = await Get<UserDto[]>(factory.Admin(), "/api/admin/users");
        Assert.Contains(users, u => u is { Email: "stranger@example.com", IsPending: true, Invited: false, Name: "Vreemde" });
    }

    [Fact]
    public async Task Configured_admin_can_do_everything()
    {
        var me = await Get<MeDto>(factory.Admin(), "/api/me");
        Assert.True(me is { IsAdmin: true, IsPending: false, IsEditor: true });
        Assert.All(await Get<SiteDto[]>(factory.Admin(), "/api/sites"), s => Assert.Equal(SiteRole.Edit, s.MyRole));
    }

    [Fact]
    public async Task Only_admins_manage_users_and_sites()
    {
        var home = await Site("Thuis");
        var editor = await Member("tuinier@example.com", new SiteRoleDto(home.Id, SiteRole.Edit));

        Assert.Equal("access.adminOnly", await Code(await editor.GetAsync("/api/admin/users", Ct), HttpStatusCode.Forbidden));
        Assert.Equal("access.adminOnly", await Code(await Post(editor, "/api/sites", new SiteInput("Nieuw", null)), HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task Invited_viewer_sees_only_their_site_and_cannot_change_it()
    {
        var home = await Site("Thuis");
        var allotment = await Site("Volkstuin");
        var viewer = await Member("kijker@example.com", new SiteRoleDto(home.Id, SiteRole.View));

        var me = await Get<MeDto>(viewer, "/api/me");
        Assert.True(me is { IsPending: false, IsAdmin: false, IsEditor: false });
        var sites = await Get<SiteDto[]>(viewer, "/api/sites");
        Assert.Equal([(home.Id, SiteRole.View)], sites.Select(s => (s.Id, s.MyRole)));

        // The first login links the invitation.
        Assert.Contains(await Get<UserDto[]>(factory.Admin(), "/api/admin/users"), u => u is { Email: "kijker@example.com", Invited: false });

        var layout = await Get<SiteLayoutDto>(viewer, $"/api/sites/{home.Id}/layout");
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/sites/{allotment.Id}/layout", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/sites/{allotment.Id}/plantings", Ct)).StatusCode);

        Assert.Equal("access.readOnly", await Code(await viewer.PutAsJsonAsync($"/api/sites/{home.Id}/layout", layout, ApiJson.Options, Ct), HttpStatusCode.Forbidden));
        Assert.Equal("access.readOnly", await Code(await Post(viewer, "/api/notes", Note(home.Id)), HttpStatusCode.Forbidden));
        Assert.Equal("access.readOnly", await Code(await Post(viewer, "/api/people", new PersonInput("Jan", "#123456")), HttpStatusCode.Forbidden));

        var tasks = await Get<TaskDto[]>(viewer, "/api/tasks?from=2026-01-01&to=2026-12-31");
        Assert.NotEmpty(tasks);
        Assert.All(tasks, t => Assert.True(t.SiteId is null || t.SiteId == home.Id));
        var first = tasks.First(t => t.SiteId == home.Id);
        Assert.Equal("access.readOnly",
            await Code(await Post(viewer, $"/api/tasks/{Uri.EscapeDataString(first.Key)}/complete", new CompleteTaskInput(20, null)), HttpStatusCode.Forbidden));
        Assert.Empty(await Get<TaskDto[]>(viewer, $"/api/tasks?siteId={allotment.Id}&from=2026-01-01&to=2026-12-31"));
    }

    [Fact]
    public async Task Editor_changes_their_site_and_shared_data_but_not_other_sites()
    {
        var home = await Site("Thuis");
        var allotment = await Site("Volkstuin");
        var editor = await Member("werker@example.com", new SiteRoleDto(home.Id, SiteRole.Edit));

        Assert.Equal(HttpStatusCode.Created, (await Post(editor, "/api/notes", Note(home.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Post(editor, "/api/people", new PersonInput("Werker", "#123456"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(editor, "/api/notes", Note(allotment.Id))).StatusCode);
        Assert.Contains(await Get<SiteDto[]>(editor, "/api/sites"), s => s.Id == home.Id && s.MyRole == SiteRole.Edit);
    }

    [Fact]
    public async Task Archived_site_is_hidden_read_only_and_can_be_restored()
    {
        var admin = factory.Admin();
        var site = await Read<SiteDto>(await Post(admin, "/api/sites", new SiteInput("Oude tuin", null)));
        var member = await Member("oud@example.com", new SiteRoleDto(site.Id, SiteRole.Edit));
        Assert.Contains(await Get<SiteDto[]>(member, "/api/sites"), s => s.Id == site.Id);

        var archived = await Read<SiteDto>(await admin.PostAsync($"/api/sites/{site.Id}/archive", null, Ct));
        Assert.True(archived.Archived);
        Assert.DoesNotContain(await Get<SiteDto[]>(member, "/api/sites"), s => s.Id == site.Id);
        Assert.DoesNotContain(await Get<SiteDto[]>(admin, "/api/sites"), s => s.Id == site.Id);
        Assert.Contains(await Get<SiteDto[]>(admin, "/api/sites?includeArchived=true"), s => s.Id == site.Id && s is { Archived: true, MyRole: SiteRole.View });
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/sites/{site.Id}/layout", Ct)).StatusCode);
        Assert.Equal("site.archived", await Code(await Post(admin, "/api/notes", Note(site.Id)), HttpStatusCode.Forbidden));

        await Read<SiteDto>(await admin.PostAsync($"/api/sites/{site.Id}/restore", null, Ct));
        Assert.Contains(await Get<SiteDto[]>(member, "/api/sites"), s => s.Id == site.Id && s.MyRole == SiteRole.Edit);
    }

    [Fact]
    public async Task Users_cannot_lock_themselves_out()
    {
        var admin = factory.Admin();
        await Get<MeDto>(admin, "/api/me");
        var self = (await Get<UserDto[]>(admin, "/api/admin/users")).Single(u => u.Email == SignedInApiFactory.AdminEmail);
        Assert.True(self.IsConfigAdmin);
        Assert.Equal("user.self", await Code(await admin.DeleteAsync($"/api/admin/users/{self.Id}", Ct), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Invitations_need_a_unique_valid_email_and_existing_sites()
    {
        var admin = factory.Admin();
        await Read<UserDto>(await Post(admin, "/api/admin/users", new UserInput("Dubbel@Example.com", "Dubbel", false, null, [])));
        Assert.Equal("user.duplicateEmail",
            await Code(await Post(admin, "/api/admin/users", new UserInput("dubbel@example.com", null, false, null, [])), HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, "/api/admin/users", new UserInput("geen-adres", null, false, null, []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await Post(admin, "/api/admin/users", new UserInput("x@example.com", null, false, null, [new SiteRoleDto(Guid.NewGuid(), SiteRole.View)]))).StatusCode);
    }
}
