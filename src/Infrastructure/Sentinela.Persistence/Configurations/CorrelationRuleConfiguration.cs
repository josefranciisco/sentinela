using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Security;

namespace Sentinela.Persistence.Configurations;

public class CorrelationRuleConfiguration : IEntityTypeConfiguration<CorrelationRule>
{
    public void Configure(EntityTypeBuilder<CorrelationRule> builder)
    {
        builder.ToTable("correlation_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.Property(r => r.ConditionExpression)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(r => r.TimeWindow)
            .IsRequired();

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("ix_correlation_rules_name");

        builder.HasIndex(r => r.IsEnabled)
            .HasDatabaseName("ix_correlation_rules_is_enabled");

        builder.Ignore(r => r.Tags);
        builder.Ignore(r => r.Actions);
    }
}
