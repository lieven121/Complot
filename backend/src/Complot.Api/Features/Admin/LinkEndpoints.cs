using Complot.Api.Auth;
using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Access;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Features.Admin;

/// <summary>An access link; it works up to and including <see cref="ValidUntil"/> (local date).</summary>
public sealed record AccessLinkDto(
    Guid Id, string Label, Guid? PersonId, DateOnly ValidUntil, bool Expired, DateTime? LastUsedAt, List<SiteRoleDto> Sites);

public sealed record AccessLinkInput(string Label, Guid? PersonId, DateOnly ValidUntil, List<SiteRoleDto> Sites);

/// <summary>The secret token is only returned when it is created; the link is <c>/auth/link/{token}</c>.</summary>
public sealed record AccessLinkCreatedDto(AccessLinkDto Link, string Token);

public sealed class AccessLinkInputValidator : AbstractValidator<AccessLinkInput>
{
    /// <summary>Volunteer links are meant to be short-lived.</summary>
    public const int MaxDays = 366;

    public AccessLinkInputValidator(ComplotDbContext db, TimeProvider time)
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ValidUntil).InclusiveBetween(time.Today(), time.Today().AddDays(MaxDays));
        RuleFor(x => x.PersonId).MustAsync((id, ct) => db.People.ExistsAsync(id!.Value, ct)).WithErrorCode("unknownPerson").When(x => x.PersonId is not null);
        RuleFor(x => x.Sites).NotEmpty().ValidSiteRoles(db);
    }
}

public static class LinkEndpoints
{
    public static void MapAccessLinks(this IEndpointRouteBuilder api)
    {
        var links = api.MapGroup("/admin/links").WithTags("Admin").RequireAdmin();
        links.MapGet("/", List).WithName("ListAccessLinks");
        links.MapPost("/", Create).WithName("CreateAccessLink").Validate<AccessLinkInput>();
        links.MapPut("/{id:guid}", Update).WithName("UpdateAccessLink").Validate<AccessLinkInput>();
        links.MapPost("/{id:guid}/token", NewToken).WithName("RenewAccessLinkToken");
        links.MapDelete("/{id:guid}", Delete).WithName("DeleteAccessLink");
    }

    private static AccessLinkDto ToDto(AccessLink l, TimeProvider time) => new(
        l.Id, l.Label, l.PersonId, ValidUntil(l.ExpiresAt, time), !l.IsValidAt(time.GetUtcNow().UtcDateTime), l.LastUsedAt,
        l.Sites.Select(s => new SiteRoleDto(s.SiteId, s.Role)).OrderBy(s => s.SiteId).ToList());

    /// <summary>A link valid until a date works until the end of that day, local time.</summary>
    private static DateTime ExpiresAt(DateOnly validUntil, TimeProvider time) =>
        TimeZoneInfo.ConvertTimeToUtc(validUntil.AddDays(1).ToDateTime(TimeOnly.MinValue), time.LocalTimeZone);

    private static DateOnly ValidUntil(DateTime expiresAt, TimeProvider time) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(expiresAt, time.LocalTimeZone)).AddDays(-1);

    /// <summary>Valid links first, soonest to expire first.</summary>
    private static async Task<AccessLinkDto[]> List(ComplotDbContext db, TimeProvider time, CancellationToken ct) =>
        (await db.AccessLinks.AsNoTracking().Include(l => l.Sites).ToListAsync(ct))
            .Select(l => ToDto(l, time))
            .OrderBy(l => l.Expired)
            .ThenBy(l => l.ValidUntil)
            .ToArray();

    private static async Task<Created<AccessLinkCreatedDto>> Create(AccessLinkInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        var (token, hash) = AccessTokens.Create();
        var link = new AccessLink { Label = "", TokenHash = hash };
        Apply(input, link, time);
        db.AccessLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/admin/links/{link.Id}", new AccessLinkCreatedDto(ToDto(link, time), token));
    }

    private static async Task<Results<Ok<AccessLinkDto>, ProblemHttpResult>> Update(
        Guid id, AccessLinkInput input, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.AccessLinks.Include(l => l.Sites).FirstOrDefaultAsync(l => l.Id == id, ct) is not { } link) return ApiProblems.NotFound();
        Apply(input, link, time);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToDto(link, time));
    }

    /// <summary>A new secret: the old link stops working (e.g. when it was shared too widely).</summary>
    private static async Task<Results<Ok<AccessLinkCreatedDto>, ProblemHttpResult>> NewToken(Guid id, ComplotDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (await db.AccessLinks.Include(l => l.Sites).FirstOrDefaultAsync(l => l.Id == id, ct) is not { } link) return ApiProblems.NotFound();
        var (token, hash) = AccessTokens.Create();
        link.TokenHash = hash;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new AccessLinkCreatedDto(ToDto(link, time), token));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(Guid id, ComplotDbContext db, CancellationToken ct)
    {
        if (await db.AccessLinks.FindAsync([id], ct) is not { } link) return ApiProblems.NotFound();
        db.AccessLinks.Remove(link);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static void Apply(AccessLinkInput input, AccessLink link, TimeProvider time)
    {
        link.Label = input.Label.Trim();
        link.PersonId = input.PersonId;
        link.ExpiresAt = ExpiresAt(input.ValidUntil, time);
        link.Sites.SyncWith(input.Sites, s => s.SiteId, s => s.SiteId,
            s => new AccessLinkSite { LinkId = link.Id, SiteId = s.SiteId, Role = s.Role },
            (s, existing) => existing.Role = s.Role);
    }
}
