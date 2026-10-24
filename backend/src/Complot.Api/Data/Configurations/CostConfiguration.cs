using Complot.Domain.Costs;
using Complot.Domain.Garden;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class CostCategoryConfiguration : IEntityTypeConfiguration<CostCategory>
{
    public void Configure(EntityTypeBuilder<CostCategory> b)
    {
        b.Property(c => c.Name).HasMaxLength(60);
        b.Property(c => c.Color).HasMaxLength(9);
        b.HasIndex(c => c.Name).IsUnique();
    }
}

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.Property(i => i.Supplier).HasMaxLength(100);
        b.Property(i => i.Number).HasMaxLength(60);
        b.Property(i => i.Notes).HasMaxLength(2000);
        b.Property(i => i.Total).HasPrecision(10, 2);
        b.Property(i => i.AttachmentFileName).HasMaxLength(100);
        b.Property(i => i.AttachmentName).HasMaxLength(200);
        b.HasIndex(i => i.Date);
        b.HasOne<Site>().WithMany().HasForeignKey(i => i.SiteId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> b)
    {
        b.Property(l => l.Description).HasMaxLength(200);
        b.Property(l => l.Amount).HasPrecision(10, 2);
        b.HasOne<CostCategory>().WithMany().HasForeignKey(l => l.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
