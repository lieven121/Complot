using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Complot.Api.Tests.Infrastructure;

/// <summary>
/// Signs a request in as the user in the <c>X-Test-User: sub;email;name</c> header, with OIDC-style claims; without
/// the header the app's own session cookie is used.
/// </summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string Header = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Without the header: the app's own session cookie (e.g. from an access link).
        if (!Request.Headers.TryGetValue(Header, out var value)) return Context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var parts = value.ToString().Split(';');
        var claims = new List<Claim> { new("sub", parts[0]) };
        if (parts.Length > 1) claims.Add(new Claim("email", parts[1]));
        if (parts.Length > 2) claims.Add(new Claim("name", parts[2]));
        var identity = new ClaimsIdentity(claims, SchemeName, "name", "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>
/// OIDC mode with the demo garden, where a client can sign in as anyone (<see cref="ClientFor"/>).
/// <see cref="AdminEmail"/> is an admin through configuration.
/// </summary>
public sealed class SignedInApiFactory : ApiFactory
{
    public const string AdminEmail = "admin@complot.test";

    protected override IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>(base.Settings)
    {
        ["Seed:DemoData"] = "true",
        ["Auth:Oidc:Authority"] = "https://login.example.invalid",
        ["Auth:Oidc:ClientId"] = "complot",
        ["Auth:Admins"] = AdminEmail,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services
            .AddAuthentication(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null));
    }

    public HttpClient ClientFor(string subject, string email, string? name = null)
    {
        var client = CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthHandler.Header, $"{subject};{email};{name ?? subject}");
        return client;
    }

    public HttpClient Admin() => ClientFor("admin-sub", AdminEmail, "Admin");

    /// <summary>A browser without a session; it keeps cookies between requests.</summary>
    public HttpClient Anonymous() => CreateClient(new() { AllowAutoRedirect = false });
}
