using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Persistence.Models;

namespace Sentinela.Persistence.Configurations;

public class ScreenshotConfiguration : IEntityTypeConfiguration<Screenshot>
{
    public void Configure(EntityTypeBuilder<Screenshot> builder)
    {
        builder.ToTable("screenshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RequestId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(s => s.User)
            .HasMaxLength(255);

        builder.Property(s => s.MonitorName)
            .HasMaxLength(128);

        builder.Property(s => s.Hash)
            .HasMaxLength(64);

        builder.Property(s => s.ImagePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(s => s.ThumbnailPath)
            .HasMaxLength(1024);

        builder.Property(s => s.MimeType)
            .HasMaxLength(32);

        builder.Property(s => s.CreatedBy)
            .HasMaxLength(255);

        builder.HasIndex(s => s.ComputerId)
            .HasDatabaseName("ix_screenshots_computer_id");

        builder.HasIndex(s => s.RequestId)
            .IsUnique()
            .HasDatabaseName("ix_screenshots_request_id");

        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("ix_screenshots_created_at");
    }
}
