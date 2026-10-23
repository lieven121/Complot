namespace Complot.Api.Data.Seed;

/// <summary>Sample catalog taken from the clickable demo. Filled in with the catalog feature.</summary>
internal static class DemoCatalog
{
    public static Task AddToAsync(ComplotDbContext db, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
