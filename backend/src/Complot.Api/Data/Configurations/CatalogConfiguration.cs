using Complot.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class PlantFamilyConfiguration : IEntityTypeConfiguration<PlantFamily>
{
    public void Configure(EntityTypeBuilder<PlantFamily> b)
    {
        b.Property(f => f.LatinName).HasMaxLength(100);
        b.Property(f => f.NameNl).HasMaxLength(100);
        b.Property(f => f.NameEn).HasMaxLength(100);
        b.HasIndex(f => f.LatinName).IsUnique();
    }
}

internal sealed class PlantCategoryConfiguration : IEntityTypeConfiguration<PlantCategory>
{
    public void Configure(EntityTypeBuilder<PlantCategory> b)
    {
        b.Property(c => c.Slug).HasMaxLength(60);
        b.Property(c => c.Name).HasMaxLength(100);
        b.Property(c => c.Icon).HasMaxLength(16);
        b.Property(c => c.Color).HasMaxLength(9);
        b.HasIndex(c => c.Slug).IsUnique();
        b.HasOne(c => c.DefaultFamily).WithMany().HasForeignKey(c => c.DefaultFamilyId).OnDelete(DeleteBehavior.Restrict);

        b.OwnsMany(c => c.Companions, companion =>
        {
            companion.ToTable("CategoryCompanions");
            companion.WithOwner().HasForeignKey(x => x.CategoryId);
            companion.HasKey(x => new { x.CategoryId, x.OtherCategoryId });
            companion.HasOne<PlantCategory>().WithMany().HasForeignKey(x => x.OtherCategoryId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class PlantVarietyConfiguration : IEntityTypeConfiguration<PlantVariety>
{
    public void Configure(EntityTypeBuilder<PlantVariety> b)
    {
        b.Property(v => v.Name).HasMaxLength(120);
        b.Property(v => v.Cultivar).HasMaxLength(120);
        b.Property(v => v.LatinName).HasMaxLength(120);
        b.Property(v => v.Notes).HasMaxLength(4000);
        b.HasIndex(v => v.Name);

        b.HasOne(v => v.Category).WithMany().HasForeignKey(v => v.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(v => v.Family).WithMany().HasForeignKey(v => v.FamilyId).OnDelete(DeleteBehavior.Restrict);

        b.ComplexProperty(v => v.SowIndoor);
        b.ComplexProperty(v => v.SowOutdoor);
        b.ComplexProperty(v => v.PlantOut);
        b.ComplexProperty(v => v.Harvest);
        b.ComplexProperty(v => v.Germination);

        b.OwnsMany(v => v.Actions, a => a.ToJson());
        b.OwnsMany(v => v.Suppliers, s =>
        {
            s.ToTable("VarietySuppliers");
            s.WithOwner().HasForeignKey(x => x.VarietyId);
            s.HasKey(x => new { x.VarietyId, x.SupplierId });
            s.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            s.Property(x => x.Unit).HasMaxLength(60);
            s.Property(x => x.Url).HasMaxLength(500);
        });
    }
}

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> b)
    {
        b.Property(s => s.Name).HasMaxLength(120);
        b.Property(s => s.Url).HasMaxLength(500);
        b.Property(s => s.Notes).HasMaxLength(2000);
        b.HasIndex(s => s.Name).IsUnique();
    }
}

internal sealed class InventoryLotConfiguration : IEntityTypeConfiguration<InventoryLot>
{
    public void Configure(EntityTypeBuilder<InventoryLot> b)
    {
        b.Property(l => l.Unit).HasMaxLength(30);
        b.Property(l => l.Notes).HasMaxLength(2000);
        b.HasOne(l => l.Variety).WithMany().HasForeignKey(l => l.VarietyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(l => l.Supplier).WithMany().HasForeignKey(l => l.SupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
