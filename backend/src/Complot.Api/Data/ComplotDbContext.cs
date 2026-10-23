using Complot.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Complot.Api.Data;

public sealed class ComplotDbContext(DbContextOptions<ComplotDbContext> options) : DbContext(options)
{
    public DbSet<PlantFamily> Families => Set<PlantFamily>();
    public DbSet<PlantCategory> Categories => Set<PlantCategory>();
    public DbSet<PlantVariety> Varieties => Set<PlantVariety>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplotDbContext).Assembly);
}
