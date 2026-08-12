using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("app_roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id");

        builder.Property(r => r.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(r => r.IsSystemRole)
            .HasColumnName("is_system_role");

        builder.Property(r => r.IsDefault)
            .HasColumnName("is_default");

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(r => r.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(r => r.IsDeleted)
            .HasColumnName("is_deleted");

        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(r => r.TenantId);
    }
}
