using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Monitoring;

namespace Sentinela.Persistence.Configurations;

public class HeartbeatConfiguration : IEntityTypeConfiguration<Heartbeat>
{
    public void Configure(EntityTypeBuilder<Heartbeat> builder)
    {
        builder.ToTable("heartbeats");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(h => h.Timestamp)
            .IsRequired();

        builder.Property(h => h.ComputerId)
            .IsRequired();

        builder.HasIndex(h => h.ComputerId)
            .HasDatabaseName("ix_heartbeats_computer_id");

        builder.HasIndex(h => h.Timestamp)
            .HasDatabaseName("ix_heartbeats_timestamp");
    }
}
