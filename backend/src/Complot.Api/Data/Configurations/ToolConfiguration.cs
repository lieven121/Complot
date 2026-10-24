using Complot.Domain.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class ToolConfiguration : IEntityTypeConfiguration<Tool>
{
    public void Configure(EntityTypeBuilder<Tool> b)
    {
        b.Property(t => t.Name).HasMaxLength(100);
        b.Property(t => t.Kind).HasMaxLength(60);
        b.Property(t => t.Icon).HasMaxLength(16);
        b.Property(t => t.Unit).HasMaxLength(40);
        b.Property(t => t.Material).HasMaxLength(100);
        b.Property(t => t.SupplierName).HasMaxLength(100);
        b.Property(t => t.Notes).HasMaxLength(2000);
        b.Property(t => t.UnitPrice).HasPrecision(10, 2);
        b.OwnsMany(t => t.Log, log => log.ToJson());
    }
}
