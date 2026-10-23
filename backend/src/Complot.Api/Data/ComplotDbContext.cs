using Complot.Domain.Catalog;
using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Data;

public sealed class ComplotDbContext(DbContextOptions<ComplotDbContext> options) : DbContext(options)
{
    public DbSet<PlantFamily> Families => Set<PlantFamily>();
    public DbSet<PlantCategory> Categories => Set<PlantCategory>();
    public DbSet<PlantVariety> Varieties => Set<PlantVariety>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Plot> Plots => Set<Plot>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<GardenPath> Paths => Set<GardenPath>();
    public DbSet<GardenFeature> Features => Set<GardenFeature>();
    public DbSet<Tree> Trees => Set<Tree>();

    public DbSet<Planting> Plantings => Set<Planting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplotDbContext).Assembly);
}
