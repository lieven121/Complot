using Complot.Domain.Plantings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class PlantingConfiguration : IEntityTypeConfiguration<Planting>
{
    public void Configure(EntityTypeBuilder<Planting> b)
    {
        b.Property(p => p.Notes).HasMaxLength(2000);
        b.HasOne(p => p.Bed).WithMany().HasForeignKey(p => p.BedId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(p => p.Variety).WithMany().HasForeignKey(p => p.VarietyId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.BedId);
    }
}
