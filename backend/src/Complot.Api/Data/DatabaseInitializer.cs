using Complot.Api.Common;
using Complot.Api.Features.Catalog.Exchange;
using Complot.Api.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Complot.Api.Data;

internal sealed class DatabaseInitializer(
    ComplotDbContext db,
    IOptions<StorageOptions> storage,
    IOptions<SeedOptions> seed,
    CatalogImporter importer,
    TimeProvider time,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(storage.Value.FullDataDir);
        await db.Database.MigrateAsync(cancellationToken);
        // WAL gives concurrent readers while writing; the setting is stored in the database file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        if (seed.Value.DemoData && !await db.Families.AnyAsync(cancellationToken))
        {
            await Seed.DemoCatalog.AddToAsync(db, importer, cancellationToken);
            logger.LogInformation("Seeded the demo plant catalog");
        }

        if (seed.Value.DemoData && !await db.Sites.AnyAsync(cancellationToken))
        {
            await Seed.DemoGarden.AddToAsync(db, time.Today(), cancellationToken);
            logger.LogInformation("Seeded the demo garden");
        }
    }
}
