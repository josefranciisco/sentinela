using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Persistence.Models;

namespace Sentinela.Persistence.Configurations;

public class SoftwareInventoryItemConfiguration : IEntityTypeConfiguration<SoftwareInventoryItem>
{
    public void Configure(EntityTypeBuilder<SoftwareInventoryItem> builder)
    {
        builder.ToTable("software_inventory");
        builder.HasKey(s => s.Id);
        builder.Property(e => e.TenantId);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(512);
        builder.Property(s => s.Version).HasMaxLength(128);
        builder.Property(s => s.Publisher).HasMaxLength(256);
        builder.Property(s => s.Category).HasMaxLength(128);
        builder.Property(s => s.InstallLocation).HasMaxLength(1024);
        builder.HasIndex(s => new { s.ComputerId, s.Name, s.Version })
            .HasDatabaseName("ix_software_inventory_computer_name_version");
    }
}

public class EndpointSecurityStatusConfiguration : IEntityTypeConfiguration<EndpointSecurityStatus>
{
    public void Configure(EntityTypeBuilder<EndpointSecurityStatus> builder)
    {
        builder.ToTable("endpoint_security_status");
        builder.HasKey(s => s.Id);
        builder.Property(e => e.TenantId);
        builder.Property(s => s.AntivirusProductName).HasMaxLength(256);
        builder.HasIndex(s => s.ComputerId)
            .IsUnique()
            .HasDatabaseName("ix_endpoint_security_status_computer_id");
    }
}
