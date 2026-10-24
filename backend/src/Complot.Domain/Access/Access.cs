using Complot.Domain.Common;

namespace Complot.Domain.Access;

public enum SiteRole
{
    View,
    Edit,
}

/// <summary>
/// Someone who signs in with OIDC. An admin can invite an e-mail address before the first login; the OIDC subject is
/// stored at that first login. A user who is not an admin and has no site yet is waiting for access.
/// </summary>
public sealed class AppUser : Entity
{
    /// <summary>Lower case; unique.</summary>
    public required string Email { get; set; }

    /// <summary>OIDC <c>sub</c>; empty for an invitation that was never used.</summary>
    public string? Subject { get; set; }

    public string? Name { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime? LastSeenAt { get; set; }

    /// <summary>The person (task assignee) this user is, if any.</summary>
    public Guid? PersonId { get; set; }

    public List<SiteAccess> Sites { get; set; } = [];

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

/// <summary>What a user may do on one site.</summary>
public sealed class SiteAccess
{
    public Guid UserId { get; set; }
    public Guid SiteId { get; set; }
    public SiteRole Role { get; set; }
}

/// <summary>
/// Who may see or change a site. Admins see every site, archived ones included (to restore or delete them); an
/// archived site is read-only for everybody and hidden from everyone else.
/// </summary>
public static class AccessPolicy
{
    public static bool CanView(bool isAdmin, SiteRole? role, bool archived) => isAdmin || (role is not null && !archived);

    public static bool CanEdit(bool isAdmin, SiteRole? role, bool archived) => !archived && (isAdmin || role == SiteRole.Edit);

    /// <summary>Waiting for access: signed in, but neither admin nor member of any site.</summary>
    public static bool IsPending(bool isAdmin, int siteCount) => !isAdmin && siteCount == 0;
}
