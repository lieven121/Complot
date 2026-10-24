using System.Security.Claims;
using Complot.Api.Data;
using Complot.Domain.Access;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Auth;

/// <summary>
/// What the caller may do: a signed-in user (<see cref="UserId"/>) or someone who opened an access link
/// (<see cref="LinkId"/>). In open mode (no OIDC) everybody is an admin.
/// </summary>
public sealed record UserAccess(
    Guid? UserId, string? Email, string? Name, bool IsAdmin, IReadOnlyDictionary<Guid, SiteRole> Roles, Guid? LinkId = null, Guid? PersonId = null)
{
    public static readonly UserAccess OpenMode = new(null, null, null, true, new Dictionary<Guid, SiteRole>());
    public static readonly UserAccess Anonymous = new(null, null, null, false, new Dictionary<Guid, SiteRole>());

    public bool IsAuthenticated => IsAdmin || UserId is not null || LinkId is not null;
    public bool IsPending => AccessPolicy.IsPending(IsAdmin, Roles.Count);

    /// <summary>May change shared data (catalog, people): admins and whoever can edit at least one site.</summary>
    public bool IsEditor => IsAdmin || Roles.Values.Contains(SiteRole.Edit);

    /// <summary>Link or unlink a person's own calendar: admins, and the person themselves (user or link for that person).</summary>
    public bool ManagesCalendarOf(Guid personId) => IsAdmin || PersonId == personId;

    public SiteRole? RoleOn(Guid siteId) => Roles.TryGetValue(siteId, out var role) ? role : null;
    public bool CanView(Guid siteId, bool archived) => AccessPolicy.CanView(IsAdmin, RoleOn(siteId), archived);
    public bool CanEdit(Guid siteId, bool archived) => AccessPolicy.CanEdit(IsAdmin, RoleOn(siteId), archived);
}

/// <summary>Admin e-mail addresses from configuration (<c>Auth:Admins</c>); they are always admin.</summary>
public sealed class AccessOptions
{
    /// <summary>Normalized e-mail addresses.</summary>
    public string[] Admins { get; init; } = [];

    public bool IsConfigAdmin(string email) => Admins.Contains(email);
}

/// <summary>
/// Resolves the signed-in user once per request: by OIDC subject, else by an invited e-mail address (then the
/// subject is stored), else a new user who waits for access.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor http, ComplotDbContext db, AuthMode mode, AccessOptions options, TimeProvider time)
{
    /// <summary>Subject of a session started with an access link: "link:{id}".</summary>
    public const string LinkSubjectPrefix = "link:";

    private static readonly TimeSpan SeenInterval = TimeSpan.FromMinutes(1);
    private UserAccess? _access;

    public async Task<UserAccess> GetAsync(CancellationToken ct)
    {
        if (_access is not null) return _access;
        if (!mode.OidcEnabled) return _access = UserAccess.OpenMode;
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || principal.FindFirstValue("sub") is not { } subject) return _access = UserAccess.Anonymous;

        if (subject.StartsWith(LinkSubjectPrefix, StringComparison.Ordinal))
            return _access = Guid.TryParse(subject[LinkSubjectPrefix.Length..], out var linkId) ? await FromLinkAsync(linkId, ct) : UserAccess.Anonymous;

        var user = await FindOrCreateAsync(subject, principal, ct);
        var isAdmin = user.IsAdmin || options.IsConfigAdmin(user.Email);
        return _access = new UserAccess(user.Id, user.Email, user.Name, isAdmin, user.Sites.ToDictionary(a => a.SiteId, a => a.Role), PersonId: user.PersonId);
    }

    /// <summary>A link session only lasts while the link exists and has not expired.</summary>
    private async Task<UserAccess> FromLinkAsync(Guid linkId, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (await db.AccessLinks.Include(l => l.Sites).FirstOrDefaultAsync(l => l.Id == linkId, ct) is not { } link || !link.IsValidAt(now))
            return UserAccess.Anonymous;
        if (!(link.LastUsedAt > now - SeenInterval))
        {
            link.LastUsedAt = now;
            await db.SaveChangesAsync(ct);
        }
        return new UserAccess(null, null, link.Label, false, link.Sites.ToDictionary(s => s.SiteId, s => s.Role), link.Id, link.PersonId);
    }

    private async Task<AppUser> FindOrCreateAsync(string subject, ClaimsPrincipal principal, CancellationToken ct)
    {
        var email = AppUser.NormalizeEmail(principal.FindFirstValue("email") ?? principal.FindFirstValue("preferred_username") ?? subject);
        var name = principal.FindFirstValue("name");
        var now = time.GetUtcNow().UtcDateTime;

        var user = await db.Users.Include(u => u.Sites).FirstOrDefaultAsync(u => u.Subject == subject, ct)
                   ?? await db.Users.Include(u => u.Sites).FirstOrDefaultAsync(u => u.Subject == null && u.Email == email, ct);
        if (user is null)
        {
            user = new AppUser { Email = email };
            db.Users.Add(user);
        }
        if (user.Subject is not null && user.LastSeenAt > now - SeenInterval) return user;

        user.Subject = subject;
        user.Name = name ?? user.Name;
        user.LastSeenAt = now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A parallel first request created this user (or the e-mail is taken): use what is stored.
            db.ChangeTracker.Clear();
            user = await db.Users.Include(u => u.Sites).AsNoTracking().FirstOrDefaultAsync(u => u.Subject == subject, ct)
                   ?? new AppUser { Email = email, Subject = subject, Name = name };
        }
        return user;
    }
}
