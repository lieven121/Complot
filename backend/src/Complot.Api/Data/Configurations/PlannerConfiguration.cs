using Complot.Domain.Planner;
using Complot.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class CalendarLinkConfiguration : IEntityTypeConfiguration<CalendarLink>
{
    public void Configure(EntityTypeBuilder<CalendarLink> b)
    {
        b.Property(l => l.AccountEmail).HasMaxLength(254);
        b.Property(l => l.ProtectedRefreshToken).HasMaxLength(4000);
        b.HasIndex(l => l.PersonId).IsUnique();
        b.HasOne<Person>().WithMany().HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Cascade);
    }
}
