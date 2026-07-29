using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Monitoring;

namespace Sentinela.Persistence.Configurations;

public class UsbEventConfiguration : IEntityTypeConfiguration<UsbEvent>
{
    public void Configure(EntityTypeBuilder<UsbEvent> builder)
    {
        builder.ToTable("usb_events");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.DeviceId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.DeviceName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.DeviceType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.SerialNumber)
            .HasMaxLength(255);

        builder.Property(u => u.VendorId)
            .HasMaxLength(50);

        builder.Property(u => u.ProductId)
            .HasMaxLength(50);

        builder.Property(u => u.Action)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(u => u.Username)
            .HasMaxLength(255);

        builder.HasIndex(u => new { u.ComputerId, u.Timestamp })
            .HasDatabaseName("ix_usb_events_computer_id_timestamp");

        builder.HasIndex(u => u.DeviceId)
            .HasDatabaseName("ix_usb_events_device_id");
    }
}
