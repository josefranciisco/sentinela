using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Persistence.Models;

namespace Sentinela.Persistence.Configurations;

public class ScreenCaptureRecordConfiguration : IEntityTypeConfiguration<ScreenCaptureRecord>
{
    public void Configure(EntityTypeBuilder<ScreenCaptureRecord> builder)
    {
        builder.ToTable("screen_capture_records");

        builder.HasKey(s => s.Id);

        builder.Property(e => e.TenantId);

        builder.Property(s => s.FilePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(s => s.ThumbnailPath)
            .HasMaxLength(1024);

        builder.Property(s => s.Username)
            .HasMaxLength(255);

        builder.HasIndex(s => new { s.ComputerId, s.CapturedAt })
            .HasDatabaseName("ix_screen_capture_records_computer_id_captured_at");

        builder.HasIndex(s => s.IsProcessed)
            .HasDatabaseName("ix_screen_capture_records_is_processed");
    }
}
