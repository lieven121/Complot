using Complot.Domain.Access;
using Complot.Domain.Garden;
using Complot.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("Users");
        b.Property(u => u.Email).HasMaxLength(254);
        b.Property(u => u.Subject).HasMaxLength(255);
        b.Property(u => u.Name).HasMaxLength(100);
        b.HasIndex(u => u.Email).IsUnique();
        b.HasIndex(u => u.Subject).IsUnique();
        b.HasOne<Person>().WithMany().HasForeignKey(u => u.PersonId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(u => u.Sites).WithOne().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SiteAccessConfiguration : IEntityTypeConfiguration<SiteAccess>
{
    public void Configure(EntityTypeBuilder<SiteAccess> b)
    {
        b.HasKey(a => new { a.UserId, a.SiteId });
        b.HasIndex(a => a.SiteId);
        b.HasOne<Site>().WithMany().HasForeignKey(a => a.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccessLinkConfiguration : IEntityTypeConfiguration<AccessLink>
{
    public void Configure(EntityTypeBuilder<AccessLink> b)
    {
        b.Property(l => l.Label).HasMaxLength(100);
        b.Property(l => l.TokenHash).HasMaxLength(64);
        b.HasIndex(l => l.TokenHash).IsUnique();
        b.HasOne<Person>().WithMany().HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(l => l.Sites).WithOne().HasForeignKey(s => s.LinkId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AccessLinkSiteConfiguration : IEntityTypeConfiguration<AccessLinkSite>
{
    public void Configure(EntityTypeBuilder<AccessLinkSite> b)
    {
        b.HasKey(s => new { s.LinkId, s.SiteId });
        b.HasOne<Site>().WithMany().HasForeignKey(s => s.SiteId).OnDelete(DeleteBehavior.Cascade);
    }
}
