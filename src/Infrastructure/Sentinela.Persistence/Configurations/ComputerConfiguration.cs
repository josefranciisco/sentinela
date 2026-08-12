using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Monitoring;

namespace Sentinela.Persistence.Configurations;

public class ComputerConfiguration : IEntityTypeConfiguration<Computer>
{
    public void Configure(EntityTypeBuilder<Computer> builder)
    {
        builder.ToTable("computers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId);

        builder.Property(c => c.Hostname)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.Domain)
            .HasMaxLength(255);

        builder.Property(c => c.IpAddress)
            .IsRequired()
            .HasMaxLength(45);

        builder.Property(c => c.MacAddress)
            .IsRequired()
            .HasMaxLength(17);

        builder.Property(c => c.OsVersion)
            .HasMaxLength(255);

        builder.Property(c => c.Department)
            .HasMaxLength(255);

        builder.Property(c => c.CurrentUser)
            .HasMaxLength(255);

        builder.Property(c => c.AgentVersion)
            .HasMaxLength(50);

        builder.Property(c => c.MonitorCount)
            .HasDefaultValue(1);

        builder.Property(c => c.Notes)
            .HasMaxLength(2000);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(c => c.Hostname)
            .HasDatabaseName("ix_computers_hostname");

        builder.HasIndex(c => c.IpAddress)
            .HasDatabaseName("ix_computers_ip_address");

        builder.HasIndex(c => c.Department)
            .HasDatabaseName("ix_computers_department");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("ix_computers_status");

        builder.HasIndex(c => c.LastHeartbeat)
            .HasDatabaseName("ix_computers_last_heartbeat");

        builder.HasMany(c => c.Heartbeats)
            .WithOne()
            .HasForeignKey(h => h.ComputerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Timeline)
            .WithOne()
            .HasForeignKey("ComputerId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.Tags);
    }
}
