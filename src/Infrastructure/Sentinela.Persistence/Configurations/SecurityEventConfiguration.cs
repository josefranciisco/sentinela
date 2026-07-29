using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Security;

namespace Sentinela.Persistence.Configurations;

public class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.ToTable("security_events");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.EventType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Category)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(s => s.Username)
            .HasMaxLength(255);

        builder.Property(s => s.SourceIp)
            .HasMaxLength(45);

        builder.Property(s => s.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null) ?? new());

        builder.HasIndex(s => new { s.ComputerId, s.Timestamp })
            .HasDatabaseName("ix_security_events_computer_id_timestamp");

        builder.HasIndex(s => s.EventType)
            .HasDatabaseName("ix_security_events_event_type");

        builder.HasIndex(s => s.Severity)
            .HasDatabaseName("ix_security_events_severity");
    }
}
