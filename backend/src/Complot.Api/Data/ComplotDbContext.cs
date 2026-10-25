using Complot.Domain.Access;
using Complot.Domain.Catalog;
using Complot.Domain.Costs;
using Complot.Domain.Garden;
using Complot.Domain.Planner;
using Complot.Domain.Plantings;
using Complot.Domain.Tools;
using Complot.Domain.Work;
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
    public DbSet<MapMarker> Markers => Set<MapMarker>();
    public DbSet<Tree> Trees => Set<Tree>();

    public DbSet<Planting> Plantings => Set<Planting>();

    public DbSet<Person> People => Set<Person>();
    public DbSet<ManualTask> ManualTasks => Set<ManualTask>();
    public DbSet<TaskState> TaskStates => Set<TaskState>();
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<SiteAccess> SiteAccess => Set<SiteAccess>();
    public DbSet<AccessLink> AccessLinks => Set<AccessLink>();

    public DbSet<CalendarLink> CalendarLinks => Set<CalendarLink>();

    public DbSet<Tool> Tools => Set<Tool>();

    public DbSet<CostCategory> CostCategories => Set<CostCategory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ComplotDbContext).Assembly);
}
