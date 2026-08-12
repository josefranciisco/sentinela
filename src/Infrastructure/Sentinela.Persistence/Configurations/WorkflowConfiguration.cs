using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Automation;

namespace Sentinela.Persistence.Configurations;

public class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> builder)
    {
        builder.ToTable("workflows");

        builder.HasKey(w => w.Id);

        builder.Property(e => e.TenantId);

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.Description)
            .HasMaxLength(2000);

        builder.Property(w => w.TriggerType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(w => w.TriggerConfig)
            .HasColumnType("jsonb");

        builder.Property(w => w.CreatedBy)
            .HasMaxLength(255);

        builder.Ignore(w => w.Actions);

        builder.HasMany(w => w.Conditions)
            .WithOne()
            .HasForeignKey("WorkflowId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany<WorkflowAction>("_actions", action =>
        {
            action.ToTable("workflow_actions");
            action.WithOwner().HasForeignKey("WorkflowId");

            action.Property<Guid>("Id")
                .ValueGeneratedOnAdd();

            action.HasKey("Id");

            action.Property(a => a.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            action.Property(a => a.Config)
                .HasColumnType("jsonb");

            action.Property(a => a.Order)
                .IsRequired();
        });

        builder.HasIndex(w => w.Name)
            .HasDatabaseName("ix_workflows_name");

        builder.HasIndex(w => w.IsEnabled)
            .HasDatabaseName("ix_workflows_is_enabled");
    }
}
