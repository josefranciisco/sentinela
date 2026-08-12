using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Alerting;

namespace Sentinela.Persistence.Configurations;

public class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(a => a.Id);

        builder.Property(e => e.TenantId);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(a => a.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(a => a.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Category)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Source)
            .HasMaxLength(255);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.AcknowledgedBy)
            .HasMaxLength(255);

        builder.Property(a => a.ResolvedBy)
            .HasMaxLength(255);

        builder.Property(a => a.Resolution)
            .HasMaxLength(4000);

        builder.HasIndex(a => new { a.ComputerId, a.RuleId, a.Status, a.Timestamp })
            .HasDatabaseName("ix_alerts_computer_rule_status_timestamp");

        builder.HasMany(a => a.Comments)
            .WithOne()
            .HasForeignKey(c => c.AlertId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
