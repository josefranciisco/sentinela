using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("app_role_permissions");

        builder.HasKey(rp => rp.Id);

        builder.Property(rp => rp.Id)
            .HasColumnName("id");

        builder.Property(rp => rp.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(rp => rp.PermissionId)
            .HasColumnName("permission_id")
            .IsRequired();

        builder.Property(rp => rp.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(rp => rp.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(rp => rp.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(rp => rp.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(rp => rp.IsDeleted)
            .HasColumnName("is_deleted");

        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(rp => rp.RoleId);

        builder.HasIndex(rp => rp.PermissionId);

        builder.HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
