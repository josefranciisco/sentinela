using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Alerting;

namespace Sentinela.Persistence.Configurations;

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("alert_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.Property(r => r.Category)
            .HasMaxLength(255);

        builder.Property(r => r.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Condition)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(255);

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("ix_alert_rules_name");

        builder.HasIndex(r => r.IsEnabled)
            .HasDatabaseName("ix_alert_rules_is_enabled");

        builder.Ignore(r => r.Tags);
    }
}
