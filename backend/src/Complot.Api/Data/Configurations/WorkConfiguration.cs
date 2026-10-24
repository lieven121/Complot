using Complot.Domain.Garden;
using Complot.Domain.Plantings;
using Complot.Domain.Work;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Complot.Api.Data.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> b)
    {
        b.ToTable("People");
        b.Property(p => p.Name).HasMaxLength(100);
        b.Property(p => p.Color).HasMaxLength(9);
        b.Property(p => p.CalendarFeedToken).HasMaxLength(64);
        b.HasIndex(p => p.CalendarFeedToken).IsUnique();
    }
}

internal sealed class ManualTaskConfiguration : IEntityTypeConfiguration<ManualTask>
{
    public void Configure(EntityTypeBuilder<ManualTask> b)
    {
        b.Property(t => t.Title).HasMaxLength(200);
        b.Ignore(t => t.SourceKey);
        b.HasOne<Site>().WithMany().HasForeignKey(t => t.SiteId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Bed>().WithMany().HasForeignKey(t => t.BedId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class TaskStateConfiguration : IEntityTypeConfiguration<TaskState>
{
    public void Configure(EntityTypeBuilder<TaskState> b)
    {
        b.Property(s => s.SourceKey).HasMaxLength(200);
        b.HasIndex(s => s.SourceKey).IsUnique();
        b.Property(s => s.CalendarTitle).HasMaxLength(200);
        b.Property(s => s.CalendarEventId).HasMaxLength(1024);
        b.Property(s => s.CalendarError).HasMaxLength(40);
        b.HasIndex(s => new { s.AssigneeId, s.ScheduledStart });
        b.HasOne<Person>().WithMany().HasForeignKey(s => s.AssigneeId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> b)
    {
        b.Property(n => n.Text).HasMaxLength(4000);
        b.Property(n => n.HarvestUnit).HasMaxLength(20);
        b.Property(n => n.PhotoFileName).HasMaxLength(100);
        b.HasIndex(n => new { n.SiteId, n.Date });
        b.HasOne<Site>().WithMany().HasForeignKey(n => n.SiteId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Bed>().WithMany().HasForeignKey(n => n.BedId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Tree>().WithMany().HasForeignKey(n => n.TreeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Planting>().WithMany().HasForeignKey(n => n.PlantingId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Person>().WithMany().HasForeignKey(n => n.PersonId).OnDelete(DeleteBehavior.SetNull);
    }
}
