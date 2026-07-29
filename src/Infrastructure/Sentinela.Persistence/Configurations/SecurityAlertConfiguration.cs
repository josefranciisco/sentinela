using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Security;

namespace Sentinela.Persistence.Configurations;

public class SecurityAlertConfiguration : IEntityTypeConfiguration<SecurityAlert>
{
    public void Configure(EntityTypeBuilder<SecurityAlert> builder)
    {
        builder.ToTable("security_alerts");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(s => s.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.Category)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Source)
            .HasMaxLength(255);

        builder.Property(s => s.Username)
            .HasMaxLength(255);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.AssignedTo)
            .HasMaxLength(255);

        builder.Property(s => s.Resolution)
            .HasMaxLength(4000);

        builder.Property(s => s.ResolvedBy)
            .HasMaxLength(255);

        builder.HasIndex(s => new { s.ComputerId, s.Severity, s.Status, s.Timestamp })
            .HasDatabaseName("ix_security_alerts_computer_severity_status_timestamp");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_security_alerts_status");

        builder.HasIndex(s => s.Timestamp)
            .HasDatabaseName("ix_security_alerts_timestamp");

        builder.Ignore(s => s.Tags);
    }
}
