using Complot.Api.Common;
using Complot.Api.Data;
using Complot.Domain.Access;
using Complot.Domain.Work;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Auth;

/// <summary>
/// Finds the site a request is about (from the route or the body). <c>null</c> means no specific site: shared data,
/// a manual task without site, or an id that does not exist (the handler then answers 404 / 400 as usual).
/// </summary>
public delegate ValueTask<Guid?> SiteResolver(EndpointFilterInvocationContext context, ComplotDbContext db, CancellationToken ct);

public static class SiteResolvers
{
    public static SiteResolver Route(string name = "id") => (context, _, _) => ValueTask.FromResult(RouteGuid(context, name));

    public static SiteResolver Body<T>(Func<T, Guid?> siteId) =>
        (context, _, _) => ValueTask.FromResult(context.Arguments.OfType<T>().FirstOrDefault() is { } body ? siteId(body) : null);

    public static SiteResolver BedInBody<T>(Func<T, Guid> bedId) => async (context, db, ct) =>
        context.Arguments.OfType<T>().FirstOrDefault() is { } body ? await SiteOfBedAsync(db, bedId(body), ct) : null;

    public static readonly SiteResolver Planting = async (context, db, ct) =>
        RouteGuid(context, "id") is { } id ? await SiteOfPlantingAsync(db, id, ct) : null;

    public static readonly SiteResolver Note = async (context, db, ct) =>
        RouteGuid(context, "id") is { } id ? await db.Notes.Where(n => n.Id == id).Select(n => (Guid?)n.SiteId).FirstOrDefaultAsync(ct) : null;

    public static readonly SiteResolver ManualTask = async (context, db, ct) =>
        RouteGuid(context, "id") is { } id ? await db.ManualTasks.Where(t => t.Id == id).Select(t => t.SiteId).FirstOrDefaultAsync(ct) : null;

    /// <summary>Task keys name their source: "planting:{id}:…", "tree:{id}:…" or "manual:{id}".</summary>
    public static readonly SiteResolver TaskKey = async (context, db, ct) =>
        context.HttpContext.Request.RouteValues["key"] is string key && TaskKeys.Parse(key) is var (source, id)
            ? source switch
            {
                "planting" => await SiteOfPlantingAsync(db, id, ct),
                "tree" => await db.Trees.Where(t => t.Id == id).Select(t => (Guid?)t.SiteId).FirstOrDefaultAsync(ct),
                "manual" => await db.ManualTasks.Where(t => t.Id == id).Select(t => t.SiteId).FirstOrDefaultAsync(ct),
                _ => null,
            }
            : null;

    private static Guid? RouteGuid(EndpointFilterInvocationContext context, string name) =>
        Guid.TryParse(context.HttpContext.Request.RouteValues[name] as string, out var id) ? id : null;

    private static Task<Guid?> SiteOfBedAsync(ComplotDbContext db, Guid bedId, CancellationToken ct) =>
        db.Beds.Where(b => b.Id == bedId).Select(b => (Guid?)b.SiteId).FirstOrDefaultAsync(ct);

    private static Task<Guid?> SiteOfPlantingAsync(ComplotDbContext db, Guid plantingId, CancellationToken ct) =>
        db.Plantings.Where(p => p.Id == plantingId).Select(p => (Guid?)p.Bed.SiteId).FirstOrDefaultAsync(ct);
}

/// <summary>Declarative access checks, added where the endpoint is mapped.</summary>
public static class AccessFilters
{
    /// <summary>
    /// The caller must be able to view (or edit) the site the request is about. A site you cannot see answers 404, so
    /// its existence does not leak; a site you may only view answers 403.
    /// </summary>
    public static RouteHandlerBuilder RequireSite(this RouteHandlerBuilder builder, SiteRole role, SiteResolver resolver) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var (db, access, ct) = await ResolveAsync(context);
            if (await resolver(context, db, ct) is not { } siteId)
                return role == SiteRole.Edit && !access.IsEditor ? ApiProblems.Forbidden("access.readOnly") : await next(context);

            var archived = await db.Sites.Where(s => s.Id == siteId).Select(s => (bool?)(s.ArchivedAt != null)).FirstOrDefaultAsync(ct);
            if (archived is not { } isArchived) return await next(context);
            if (!access.CanView(siteId, isArchived)) return ApiProblems.NotFound();
            if (role == SiteRole.Edit && !access.CanEdit(siteId, isArchived))
                return ApiProblems.Forbidden(isArchived ? "site.archived" : "access.readOnly");
            return await next(context);
        }).ProducesProblem(StatusCodes.Status403Forbidden);

    public static TBuilder RequireAdmin<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
            (await ResolveAsync(context)).Access.IsAdmin ? await next(context) : ApiProblems.Forbidden("access.adminOnly"));

    /// <summary>Shared data (catalog, people): everyone may read; changing it needs edit rights on at least one site.</summary>
    public static TBuilder RequireEditorForWrites<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
            HttpMethods.IsGet(context.HttpContext.Request.Method) || (await ResolveAsync(context)).Access.IsEditor
                ? await next(context)
                : ApiProblems.Forbidden("access.readOnly"));

    /// <summary>Signed in but not yet given access: only <c>/api/me</c> answers.</summary>
    public static TBuilder RequireActiveUser<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
            (await ResolveAsync(context)).Access.IsPending ? ApiProblems.Forbidden("access.pending") : await next(context));

    private static async Task<(ComplotDbContext Db, UserAccess Access, CancellationToken Ct)> ResolveAsync(EndpointFilterInvocationContext context)
    {
        var services = context.HttpContext.RequestServices;
        var ct = context.HttpContext.RequestAborted;
        return (services.GetRequiredService<ComplotDbContext>(), await services.GetRequiredService<CurrentUser>().GetAsync(ct), ct);
    }
}
