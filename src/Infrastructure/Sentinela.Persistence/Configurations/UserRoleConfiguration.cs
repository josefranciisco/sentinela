using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Identity;

namespace Sentinela.Persistence.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("app_user_roles");

        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.Id)
            .HasColumnName("id");

        builder.Property(ur => ur.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(ur => ur.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.Property(ur => ur.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(ur => ur.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(ur => ur.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(ur => ur.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(ur => ur.IsDeleted)
            .HasColumnName("is_deleted");

        builder.HasIndex(ur => new { ur.UserId, ur.RoleId })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(ur => ur.UserId);

        builder.HasIndex(ur => ur.RoleId);

        builder.HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
