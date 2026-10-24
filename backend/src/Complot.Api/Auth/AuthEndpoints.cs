using System.Security.Claims;
using Complot.Api.Data;
using Complot.Domain.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

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
public sealed record MeDto(
    AuthModeKind AuthMode, bool IsAuthenticated, string? Name, string? Email, bool IsAdmin, bool IsPending, bool IsEditor, Guid? PersonId = null);

public static class AuthEndpoints
{
    /// <summary><c>GET /api/me</c>: who is signed in, and whether sign-in is used at all.</summary>
    public static void MapMeEndpoint(this IEndpointRouteBuilder api) =>
        api.MapGet("/me", async (CurrentUser current, AuthMode mode, CancellationToken ct) =>
            {
                var user = await current.GetAsync(ct);
                return new MeDto(mode.OidcEnabled ? AuthModeKind.Oidc : AuthModeKind.None, user.IsAuthenticated, user.Name, user.Email,
                    user.IsAdmin, user.IsAuthenticated && user.IsPending, user.IsEditor, user.PersonId);
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

        // A session from an access link has no identity-provider session to end.
        auth.MapPost("/logout", Results<SignOutHttpResult, RedirectHttpResult> (AuthMode mode, ClaimsPrincipal user) => mode.OidcEnabled
            ? TypedResults.SignOut(new AuthenticationProperties { RedirectUri = "/" }, IsLinkSession(user)
                ? [CookieAuthenticationDefaults.AuthenticationScheme]
                : [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme])
            : TypedResults.LocalRedirect("/")).AllowAnonymous();

        auth.MapGet("/link/{token}", OpenLink).AllowAnonymous();
    }

    /// <summary>
    /// Opening an access link signs the browser in with the link's rights until the link expires; the session is
    /// re-checked on every request, so deleting the link ends it at once.
    /// </summary>
    private static async Task<IResult> OpenLink(string token, AuthMode mode, ComplotDbContext db, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        if (!mode.OidcEnabled) return TypedResults.LocalRedirect("/");
        var hash = AccessTokens.Hash(token);
        var link = await db.AccessLinks.AsNoTracking().FirstOrDefaultAsync(l => l.TokenHash == hash, ct);
        if (link is null || !link.IsValidAt(time.GetUtcNow().UtcDateTime))
            return TypedResults.Content(LinkInvalidPage, "text/html; charset=utf-8", statusCode: StatusCodes.Status410Gone);

        var identity = new ClaimsIdentity(
            [new Claim("sub", CurrentUser.LinkSubjectPrefix + link.Id), new Claim("name", link.Label)],
            CookieAuthenticationDefaults.AuthenticationScheme, "name", "role");
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = link.ExpiresAt, AllowRefresh = false });
        return TypedResults.LocalRedirect("/taken");
    }

    private static bool IsLinkSession(ClaimsPrincipal user) =>
        user.FindFirstValue("sub")?.StartsWith(CurrentUser.LinkSubjectPrefix, StringComparison.Ordinal) == true;

    /// <summary>Shown outside the app (the visitor has no session), so in both languages.</summary>
    private const string LinkInvalidPage = """
        <!doctype html><html lang="nl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>Complot</title><style>body{font-family:system-ui,sans-serif;background:#f4f1e8;color:#2a2a24;display:grid;place-items:center;min-height:90vh;margin:0}
        main{background:#fffdf8;border:1px solid #e4ddcc;border-radius:12px;padding:28px 26px;max-width:440px;text-align:center}p{color:#6f6a5c}</style></head>
        <body><main><div style="font-size:40px">🌱</div><h1>Deze link werkt niet meer</h1>
        <p>De toegangslink is verlopen of ingetrokken. Vraag een nieuwe link aan de beheerder van de tuin.</p>
        <p lang="en"><i>This access link has expired or was revoked. Ask the garden's admin for a new one.</i></p></main></body></html>
        """;

    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal) && !url.StartsWith("/\\", StringComparison.Ordinal);
}
