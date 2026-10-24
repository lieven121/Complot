using System.Net;
using System.Net.Http.Json;
using Complot.Api.Auth;
using Complot.Api.Features.Admin;
using Complot.Api.Features.Garden;
using Complot.Api.Features.Work;
using Complot.Api.Tests.Infrastructure;
using Complot.Domain.Access;

namespace Complot.Api.Tests.Access;

public sealed class AccessLinkTests(SignedInApiFactory factory) : IClassFixture<SignedInApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, Ct))!;
    }

    private async Task<SiteDto> Site(string name) =>
        (await Read<SiteDto[]>(await factory.Admin().GetAsync("/api/sites", Ct))).Single(s => s.Name == name);

    private async Task<AccessLinkCreatedDto> CreateLink(string label, params SiteRoleDto[] sites) =>
        await Read<AccessLinkCreatedDto>(await factory.Admin().PostAsJsonAsync("/api/admin/links",
            new AccessLinkInput(label, null, Today.AddDays(7), [.. sites]), ApiJson.Options, Ct));

    /// <summary>Opens the link like a browser and returns that browser, now signed in.</summary>
    private async Task<HttpClient> Open(string token)
    {
        var browser = factory.Anonymous();
        var response = await browser.GetAsync($"/auth/link/{token}", Ct);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/taken", response.Headers.Location?.OriginalString);
        return browser;
    }

    [Fact]
    public async Task Link_gives_its_rights_without_an_account()
    {
        var home = await Site("Thuis");
        var created = await CreateLink("Vrijwilligers zaterdag", new SiteRoleDto(home.Id, SiteRole.Edit));
        Assert.Equal(Today.AddDays(7), created.Link.ValidUntil);
        Assert.False(created.Link.Expired);

        var volunteer = await Open(created.Token);
        var me = await Read<MeDto>(await volunteer.GetAsync("/api/me", Ct));
        Assert.True(me is { IsAuthenticated: true, IsPending: false, IsAdmin: false, Name: "Vrijwilligers zaterdag" });
        Assert.Equal([home.Id], (await Read<SiteDto[]>(await volunteer.GetAsync("/api/sites", Ct))).Select(s => s.Id));

        var task = (await Read<TaskDto[]>(await volunteer.GetAsync($"/api/tasks?siteId={home.Id}&from=2026-01-01&to=2026-12-31", Ct))).First(t => !t.Done);
        await Read<TaskDto>(await volunteer.PostAsJsonAsync($"/api/tasks/{Uri.EscapeDataString(task.Key)}/complete", new CompleteTaskInput(30, null), ApiJson.Options, Ct));

        // No account was created, and the admin page shows when the link was used.
        Assert.DoesNotContain(await Read<UserDto[]>(await factory.Admin().GetAsync("/api/admin/users", Ct)), u => u.Name == "Vrijwilligers zaterdag");
        Assert.NotNull((await Read<AccessLinkDto[]>(await factory.Admin().GetAsync("/api/admin/links", Ct))).Single(l => l.Id == created.Link.Id).LastUsedAt);
    }

    [Fact]
    public async Task Deleting_or_renewing_a_link_ends_access_at_once()
    {
        var home = await Site("Thuis");
        var deleted = await CreateLink("Eenmalig", new SiteRoleDto(home.Id, SiteRole.View));
        var browser = await Open(deleted.Token);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/sites", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin().DeleteAsync($"/api/admin/links/{deleted.Link.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/sites", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Gone, (await factory.Anonymous().GetAsync($"/auth/link/{deleted.Token}", Ct)).StatusCode);

        var renewed = await CreateLink("Gedeeld", new SiteRoleDto(home.Id, SiteRole.View));
        var fresh = await Read<AccessLinkCreatedDto>(await factory.Admin().PostAsync($"/api/admin/links/{renewed.Link.Id}/token", null, Ct));
        Assert.Equal(HttpStatusCode.Gone, (await factory.Anonymous().GetAsync($"/auth/link/{renewed.Token}", Ct)).StatusCode);
        await Open(fresh.Token);
    }

    [Fact]
    public async Task View_only_link_cannot_change_anything()
    {
        var home = await Site("Thuis");
        var browser = await Open((await CreateLink("Kijkers", new SiteRoleDto(home.Id, SiteRole.View))).Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.PostAsJsonAsync("/api/people", new PersonInput("X", "#123456"), ApiJson.Options, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync("/api/admin/links", Ct)).StatusCode);
    }

    [Fact]
    public async Task Links_need_a_site_a_label_and_a_short_lifetime()
    {
        var home = await Site("Thuis");
        var admin = factory.Admin();
        foreach (var input in new[]
                 {
                     new AccessLinkInput("Geen locatie", null, Today.AddDays(7), []),
                     new AccessLinkInput("", null, Today.AddDays(7), [new SiteRoleDto(home.Id, SiteRole.View)]),
                     new AccessLinkInput("Gisteren", null, Today.AddDays(-1), [new SiteRoleDto(home.Id, SiteRole.View)]),
                     new AccessLinkInput("Te lang", null, Today.AddYears(2), [new SiteRoleDto(home.Id, SiteRole.View)]),
                 })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/links", input, ApiJson.Options, Ct)).StatusCode);
    }
}
