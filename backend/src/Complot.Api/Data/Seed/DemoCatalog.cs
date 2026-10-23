using System.Text.Json;
using Complot.Api.Features.Catalog.Exchange;
using Complot.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Data.Seed;

/// <summary>
/// Sample catalog from the clickable demo (demo-catalog.json, embedded). Categories and varieties go through the
/// regular <see cref="CatalogImporter"/>, so the seed is validated exactly like user imports.
/// </summary>
internal static class DemoCatalog
{
    private sealed record SeedFile(List<PlantFamily> Families, List<SeedSupplier> Suppliers, CatalogExchange Catalog, List<SeedLot> Lots);
    private sealed record SeedSupplier(string Name, bool IsOwnSource);
    private sealed record SeedLot(string Variety, string? Cultivar, string Supplier, decimal Quantity, string Unit, DateOnly BoughtOn, DateOnly? BestBefore);

    public static async Task AddToAsync(ComplotDbContext db, CatalogImporter importer, CancellationToken ct)
    {
        var seed = await ReadSeedFileAsync(ct);

        db.Families.AddRange(seed.Families);
        db.Suppliers.AddRange(seed.Suppliers.Select(s => new Supplier { Name = s.Name, IsOwnSource = s.IsOwnSource }));
        await db.SaveChangesAsync(ct);

        var report = await importer.RunAsync(seed.Catalog, commit: true, ct);
        var failed = report.Categories.Concat(report.Varieties).Where(r => r.Status == ImportRowStatus.Error).ToList();
        if (failed.Count > 0)
            throw new InvalidOperationException($"Demo catalog has invalid rows: {string.Join(", ", failed.Select(r => r.Name))}");

        var varieties = await db.Varieties.ToDictionaryAsync(v => (v.Name, v.Cultivar), ct);
        var suppliers = await db.Suppliers.ToDictionaryAsync(s => s.Name, ct);
        db.InventoryLots.AddRange(seed.Lots.Select(l => new InventoryLot
        {
            VarietyId = varieties[(l.Variety, l.Cultivar)].Id,
            SupplierId = suppliers[l.Supplier].Id,
            Quantity = l.Quantity,
            Unit = l.Unit,
            BoughtOn = l.BoughtOn,
            BestBefore = l.BestBefore,
        }));
        await db.SaveChangesAsync(ct);
    }

    private static async Task<SeedFile> ReadSeedFileAsync(CancellationToken ct)
    {
        await using var stream = typeof(DemoCatalog).Assembly.GetManifestResourceStream("Complot.Api.Data.Seed.demo-catalog.json")
            ?? throw new InvalidOperationException("Embedded demo-catalog.json not found");
        return await JsonSerializer.DeserializeAsync<SeedFile>(stream, JsonSerializerOptions.Web, ct)
            ?? throw new InvalidOperationException("demo-catalog.json is empty");
    }
}
