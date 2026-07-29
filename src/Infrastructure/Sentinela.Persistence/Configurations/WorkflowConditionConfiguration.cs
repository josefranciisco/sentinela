using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Automation;

namespace Sentinela.Persistence.Configurations;

public class WorkflowConditionConfiguration : IEntityTypeConfiguration<WorkflowCondition>
{
    public void Configure(EntityTypeBuilder<WorkflowCondition> builder)
    {
        builder.ToTable("workflow_conditions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Field)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.Operator)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.Value)
            .IsRequired()
            .HasMaxLength(4000);
    }
}
