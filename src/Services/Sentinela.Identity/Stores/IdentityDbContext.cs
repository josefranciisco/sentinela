using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sentinela.Identity.Models;

namespace Sentinela.Identity.Stores;

public class IdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("refresh_tokens");

            entity.HasKey(rt => rt.Id);

            entity.Property(rt => rt.Token)
                .HasMaxLength(512)
                .IsRequired();

            entity.Property(rt => rt.DeviceInfo)
                .HasMaxLength(256);

            entity.Property(rt => rt.IpAddress)
                .HasMaxLength(45);

            entity.HasIndex(rt => rt.Token)
                .IsUnique()
                .HasDatabaseName("ix_refresh_tokens_token");

            entity.HasIndex(rt => rt.UserId)
                .HasDatabaseName("ix_refresh_tokens_user_id");

            entity.HasOne<ApplicationUser>()
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");

            entity.Property(u => u.Department).HasMaxLength(128);
            entity.Property(u => u.FullName).HasMaxLength(256);
            entity.Property(u => u.TwoFactorSecret).HasMaxLength(128);
            entity.Property(u => u.RecoveryCodesHash).HasMaxLength(2048);
            entity.Property(u => u.SsoProvider).HasMaxLength(64);
            entity.Property(u => u.SsoSubjectId).HasMaxLength(256);

            entity.HasIndex(u => u.SsoSubjectId).HasDatabaseName("ix_users_sso_subject_id");
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("roles");
        });

        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetColumnType("timestamp with time zone");
                }
            }

            entity.SetTableName(ToSnakeCase(entity.GetTableName() ?? entity.ClrType.Name));
        }

        foreach (var relationship in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        return string.Concat(
            input.Select((c, i) =>
                i > 0 && char.IsUpper(c)
                    ? "_" + char.ToLowerInvariant(c)
                    : char.ToLowerInvariant(c).ToString()
            )
        );
    }
}
