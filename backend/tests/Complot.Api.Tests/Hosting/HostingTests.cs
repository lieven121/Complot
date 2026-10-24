using System.Net;
using System.Net.Http.Json;
using Complot.Api.Auth;
using Complot.Api.Tests.Infrastructure;

namespace Complot.Api.Tests.Hosting;

public sealed class HostingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Without_oidc_everyone_is_a_signed_in_admin()
    {
        var me = await _client.GetFromJsonAsync<MeDto>("/api/me", ApiJson.Options, Ct);
        Assert.Equal(new MeDto(AuthModeKind.None, true, null, null, IsAdmin: true, IsPending: false, IsEditor: true), me);
    }

    [Fact]
    public async Task Unknown_api_routes_return_a_problem_404()
    {
        var response = await _client.GetAsync("/api/does-not-exist", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

public sealed class OidcHostingTests(OidcApiFactory factory) : IClassFixture<OidcApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Api_calls_without_a_session_get_401_instead_of_a_redirect()
    {
        var response = await _client.GetAsync("/api/families", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_is_public_and_reports_oidc_mode()
    {
        var me = await _client.GetFromJsonAsync<MeDto>("/api/me", ApiJson.Options, Ct);
        Assert.Equal(new MeDto(AuthModeKind.Oidc, false, null, null, IsAdmin: false, IsPending: false, IsEditor: false), me);
    }

    [Fact]
    public async Task Health_stays_public() =>
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health", Ct)).StatusCode);
}
