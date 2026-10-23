using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Complot.Api.Auth;

public sealed class OidcOptions
{
    public const string Section = "Auth:Oidc";

    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);
}

/// <summary>Whether sign-in is required. Without OIDC configuration the app is open (internal use).</summary>
public sealed record AuthMode(bool OidcEnabled);

public static class AuthSetup
{
    public static IServiceCollection AddComplotAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var oidc = configuration.GetSection(OidcOptions.Section).Get<OidcOptions>() ?? new OidcOptions();
        services.AddSingleton(new AuthMode(oidc.IsConfigured));

        if (!oidc.IsConfigured)
        {
            services.AddAuthentication();
            services.AddAuthorization();
            return services;
        }

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "complot.auth";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.LoginPath = "/auth/login";
                // API calls get a plain 401/403; pages are redirected to the login flow.
                options.Events.OnRedirectToLogin = context => ApiAwareRedirect(context, StatusCodes.Status401Unauthorized);
                options.Events.OnRedirectToAccessDenied = context => ApiAwareRedirect(context, StatusCodes.Status403Forbidden);
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = oidc.Authority;
                options.ClientId = oidc.ClientId;
                options.ClientSecret = oidc.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "name";
                options.Scope.Clear();
                foreach (var scope in oidc.Scopes) options.Scope.Add(scope);
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }

    private static Task ApiAwareRedirect(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context, int apiStatus)
    {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = apiStatus;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    }
}
