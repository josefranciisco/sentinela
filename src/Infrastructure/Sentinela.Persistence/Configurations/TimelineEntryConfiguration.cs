using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Monitoring;

namespace Sentinela.Persistence.Configurations;

public class TimelineEntryConfiguration : IEntityTypeConfiguration<TimelineEntry>
{
    public void Configure(EntityTypeBuilder<TimelineEntry> builder)
    {
        builder.ToTable("timeline_entries");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Category)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(t => t.Username)
            .HasMaxLength(255);

        builder.Property(t => t.Details)
            .HasMaxLength(4000);

        builder.Property(t => t.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(t => new { t.ComputerId, t.Timestamp })
            .HasDatabaseName("ix_timeline_entries_computer_id_timestamp");

        builder.HasIndex(t => t.EventType)
            .HasDatabaseName("ix_timeline_entries_event_type");

        builder.HasIndex(t => t.Username)
            .HasDatabaseName("ix_timeline_entries_username");
    }
}
