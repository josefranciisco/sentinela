using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Audit;

namespace Sentinela.Persistence.Configurations;

public class AuditTrailConfiguration : IEntityTypeConfiguration<AuditTrail>
{
    public void Configure(EntityTypeBuilder<AuditTrail> builder)
    {
        builder.ToTable("audit_trails");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.Resource)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(a => a.ResourceId)
            .HasMaxLength(255);

        builder.Property(a => a.Details)
            .HasMaxLength(4000);

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45);

        builder.Property(a => a.UserAgent)
            .HasMaxLength(512);

        builder.Property(a => a.TenantId)
            .HasMaxLength(255);

        builder.HasIndex(a => new { a.UserId, a.Timestamp, a.Action })
            .HasDatabaseName("ix_audit_trails_user_id_timestamp_action");

        builder.HasIndex(a => a.Timestamp)
            .HasDatabaseName("ix_audit_trails_timestamp");

        builder.HasIndex(a => a.Resource)
            .HasDatabaseName("ix_audit_trails_resource");

        builder.HasIndex(a => a.Action)
            .HasDatabaseName("ix_audit_trails_action");
    }
}
