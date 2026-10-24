using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Access;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Admin;

public sealed record SiteRoleDto(Guid SiteId, SiteRole Role);

/// <summary>
/// A user as the admin page shows it. <see cref="IsConfigAdmin"/>: admin through configuration (<c>Auth:Admins</c>),
/// so the admin switch does not apply. <see cref="Invited"/>: never signed in yet.
/// </summary>
public sealed record UserDto(
    Guid Id, string Email, string? Name, bool IsAdmin, bool IsConfigAdmin, bool IsPending, bool Invited,
    DateTime? LastSeenAt, Guid? PersonId, List<SiteRoleDto> Sites);

public sealed record UserInput(string Email, string? Name, bool IsAdmin, Guid? PersonId, List<SiteRoleDto> Sites);

public sealed class UserInputValidator : AbstractValidator<UserInput>
{
    public UserInputValidator(ComplotDbContext db)
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Name).MaximumLength(100);
        RuleFor(x => x.PersonId).MustAsync((id, ct) => db.People.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownPerson").When(x => x.PersonId is not null);
        RuleFor(x => x.Sites).ValidSiteRoles(db);
    }
}

public sealed class SiteRoleDtoValidator : AbstractValidator<SiteRoleDto>
{
    public SiteRoleDtoValidator(ComplotDbContext db)
    {
        RuleFor(x => x.SiteId).MustAsync((id, ct) => db.Sites.ExistsAsync(id, ct)).WithErrorCode("unknownSite");
        RuleFor(x => x.Role).IsInEnum();
    }
}

public static class SiteRoleRules
{
    /// <summary>Roles per site, for users and access links: each site once, and only existing sites.</summary>
    public static void ValidSiteRoles<T>(this IRuleBuilder<T, List<SiteRoleDto>> rule, ComplotDbContext db) =>
        rule.Must(s => s.Select(x => x.SiteId).Distinct().Count() == s.Count).WithErrorCode("duplicateSite")
            .ForEach(site => site.SetValidator(new SiteRoleDtoValidator(db)));
}

public static class UserEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder api)
    {
        api.MapAccessLinks();

        var users = api.MapGroup("/admin/users").WithTags("Admin").RequireAdmin();
        users.MapGet("/", List).WithName("ListUsers");
        users.MapPost("/", Create).WithName("InviteUser").Validate<UserInput>();
        users.MapPut("/{id:guid}", Update).WithName("UpdateUser").Validate<UserInput>();
        users.MapDelete("/{id:guid}", Delete).WithName("DeleteUser");
    }

    private static UserDto ToDto(AppUser u, AccessOptions options)
    {
        var configAdmin = options.IsConfigAdmin(u.Email);
        return new UserDto(u.Id, u.Email, u.Name, u.IsAdmin || configAdmin, configAdmin, AccessPolicy.IsPending(u.IsAdmin || configAdmin, u.Sites.Count),
            u.Subject is null, u.LastSeenAt, u.PersonId, u.Sites.Select(s => new SiteRoleDto(s.SiteId, s.Role)).OrderBy(s => s.SiteId).ToList());
    }

    /// <summary>Waiting users first, then by name.</summary>
    private static async Task<UserDto[]> List(ComplotDbContext db, AccessOptions options, CancellationToken ct) =>
        (await db.Users.AsNoTracking().Include(u => u.Sites).ToListAsync(ct))
            .Select(u => ToDto(u, options))
            .OrderByDescending(u => u.IsPending)
            .ThenBy(u => u.Name ?? u.Email, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    private static async Task<Results<Created<UserDto>, ProblemHttpResult>> Create(UserInput input, ComplotDbContext db, AccessOptions options, CancellationToken ct)
    {
        var user = new AppUser { Email = "" };
        Apply(input, user);
        db.Users.Add(user);
        if (await db.SaveOrConflictAsync("user.duplicateEmail", ct) is { } conflict) return conflict;
        return TypedResults.Created($"/api/admin/users/{user.Id}", ToDto(user, options));
    }

    private static async Task<Results<Ok<UserDto>, ProblemHttpResult>> Update(
        Guid id, UserInput input, ComplotDbContext db, AccessOptions options, CurrentUser current, CancellationToken ct)
    {
        if (await db.Users.Include(u => u.Sites).FirstOrDefaultAsync(u => u.Id == id, ct) is not { } user) return ApiProblems.NotFound();
        var me = await current.GetAsync(ct);
        var staysAdmin = input.IsAdmin || options.IsConfigAdmin(AppUser.NormalizeEmail(input.Email));
        if (!staysAdmin && user.Id == me.UserId) return ApiProblems.Conflict("user.self");
        if (user.IsAdmin && !staysAdmin && !await OtherAdminExistsAsync(user.Id, db, options, ct)) return ApiProblems.Conflict("user.lastAdmin");

        Apply(input, user);
        if (await db.SaveOrConflictAsync("user.duplicateEmail", ct) is { } conflict) return conflict;
        return TypedResults.Ok(ToDto(user, options));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(
        Guid id, ComplotDbContext db, AccessOptions options, CurrentUser current, CancellationToken ct)
    {
        if (await db.Users.FindAsync([id], ct) is not { } user) return ApiProblems.NotFound();
        if (user.Id == (await current.GetAsync(ct)).UserId) return ApiProblems.Conflict("user.self");
        if (user.IsAdmin && !await OtherAdminExistsAsync(user.Id, db, options, ct)) return ApiProblems.Conflict("user.lastAdmin");
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>Someone else stays admin: a configured admin address, or another user with the admin flag.</summary>
    private static async Task<bool> OtherAdminExistsAsync(Guid userId, ComplotDbContext db, AccessOptions options, CancellationToken ct) =>
        options.Admins.Length > 0 || await db.Users.AnyAsync(u => u.Id != userId && u.IsAdmin, ct);

    private static void Apply(UserInput input, AppUser user)
    {
        user.Email = AppUser.NormalizeEmail(input.Email);
        user.Name = string.IsNullOrWhiteSpace(input.Name) ? user.Name : input.Name.Trim();
        user.IsAdmin = input.IsAdmin;
        user.PersonId = input.PersonId;
        user.Sites.SyncWith(input.Sites, a => a.SiteId, s => s.SiteId,
            s => new SiteAccess { UserId = user.Id, SiteId = s.SiteId, Role = s.Role },
            (s, a) => a.Role = s.Role);
    }
}
