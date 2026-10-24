using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Complot.Api.Auth;

public enum AuthModeKind
{
    None,
    Oidc,
}

/// <summary>
/// The caller. <see cref="IsPending"/>: signed in but not given access yet. <see cref="IsEditor"/>: may change shared data
/// such as the catalog. Rights per site are on each site (<c>SiteDto.MyRole</c>).
/// </summary>
public sealed record MeDto(AuthModeKind AuthMode, bool IsAuthenticated, string? Name, string? Email, bool IsAdmin, bool IsPending, bool IsEditor);

public static class AuthEndpoints
{
    /// <summary><c>GET /api/me</c>: who is signed in, and whether sign-in is used at all.</summary>
    public static void MapMeEndpoint(this IEndpointRouteBuilder api) =>
        api.MapGet("/me", async (CurrentUser current, AuthMode mode, CancellationToken ct) =>
            {
                var user = await current.GetAsync(ct);
                return new MeDto(mode.OidcEnabled ? AuthModeKind.Oidc : AuthModeKind.None, user.IsAuthenticated, user.Name, user.Email,
                    user.IsAdmin, user.IsAuthenticated && user.IsPending, user.IsEditor);
            })
            .WithName("GetMe")
            .WithTags("Auth")
            .AllowAnonymous();

    /// <summary><c>/auth/login</c> and <c>/auth/logout</c> drive the OIDC browser flow.</summary>
    public static void MapAuthRoutes(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").ExcludeFromDescription();

        auth.MapGet("/login", Results<ChallengeHttpResult, RedirectHttpResult> (string? returnUrl, AuthMode mode) =>
        {
            var target = IsLocalUrl(returnUrl) ? returnUrl! : "/";
            return mode.OidcEnabled
                ? TypedResults.Challenge(new AuthenticationProperties { RedirectUri = target }, [OpenIdConnectDefaults.AuthenticationScheme])
                : TypedResults.LocalRedirect(target);
        }).AllowAnonymous();

        auth.MapPost("/logout", Results<SignOutHttpResult, RedirectHttpResult> (AuthMode mode) => mode.OidcEnabled
            ? TypedResults.SignOut(new AuthenticationProperties { RedirectUri = "/" },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme])
            : TypedResults.LocalRedirect("/"));
    }

    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal);
}
