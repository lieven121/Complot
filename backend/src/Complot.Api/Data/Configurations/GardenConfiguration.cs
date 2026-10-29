using Complot.Domain.Garden;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> b)
    {
        b.Property(s => s.Name).HasMaxLength(100);
        b.Property(s => s.Notes).HasMaxLength(2000);
    }
}

internal sealed class PlotConfiguration : IEntityTypeConfiguration<Plot>
{
    public void Configure(EntityTypeBuilder<Plot> b)
    {
        b.Property(p => p.Name).HasMaxLength(100);
        b.ComplexProperty(p => p.Shape);
        b.HasOne<Site>().WithMany().HasForeignKey(p => p.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BedConfiguration : IEntityTypeConfiguration<Bed>
{
    public void Configure(EntityTypeBuilder<Bed> b)
    {
        b.Property(x => x.Code).HasMaxLength(12);
        b.Property(x => x.Soil).HasMaxLength(200);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.ComplexProperty(x => x.Shape);
        b.HasIndex(x => new { x.SiteId, x.Code }).IsUnique();
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Plot>().WithMany().HasForeignKey(x => x.PlotId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GardenPathConfiguration : IEntityTypeConfiguration<GardenPath>
{
    public void Configure(EntityTypeBuilder<GardenPath> b)
    {
        b.ToTable("Paths");
        b.Property(p => p.Name).HasMaxLength(100);
        b.OwnsMany(p => p.Points, p => p.ToJson());
        b.HasOne<Site>().WithMany().HasForeignKey(p => p.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GardenFeatureConfiguration : IEntityTypeConfiguration<GardenFeature>
{
    public void Configure(EntityTypeBuilder<GardenFeature> b)
    {
        b.ToTable("Features");
        b.Property(f => f.Name).HasMaxLength(100);
        b.ComplexProperty(f => f.Shape);
        b.HasOne<Site>().WithMany().HasForeignKey(f => f.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MapMarkerConfiguration : IEntityTypeConfiguration<MapMarker>
{
    public void Configure(EntityTypeBuilder<MapMarker> b)
    {
        b.ToTable("Markers");
        b.Property(m => m.Label).HasMaxLength(100);
        b.Property(m => m.Color).HasMaxLength(7);
        b.OwnsMany(m => m.Points, p => p.ToJson());
        b.HasOne<Site>().WithMany().HasForeignKey(m => m.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TreeConfiguration : IEntityTypeConfiguration<Tree>
{
    public void Configure(EntityTypeBuilder<Tree> b)
    {
        b.Property(t => t.Name).HasMaxLength(100);
        b.Property(t => t.Cultivar).HasMaxLength(120);
        b.OwnsMany(t => t.Actions, a => a.ToJson());
        b.ComplexProperty(t => t.Pollination, p => p.Property(x => x.Group).HasMaxLength(60));
        b.Ignore(t => t.AllActions);
        b.Ignore(t => t.EffectivePollination);
        b.HasOne(t => t.Variety).WithMany().HasForeignKey(t => t.VarietyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(t => t.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}
