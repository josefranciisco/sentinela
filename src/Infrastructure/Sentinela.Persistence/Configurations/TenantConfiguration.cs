using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Tenant;

namespace Sentinela.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.CNPJ)
            .HasMaxLength(18);

        builder.Property(t => t.Plan)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(t => t.CNPJ)
            .HasDatabaseName("ix_tenants_cnpj");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_tenants_status");
    }
}
