using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Persistence.Models;

namespace Sentinela.Persistence.Configurations;

public class ScreenCaptureConfiguration : IEntityTypeConfiguration<ScreenCapture>
{
    public void Configure(EntityTypeBuilder<ScreenCapture> builder)
    {
        builder.ToTable("screen_captures");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.RequestedBy)
            .HasMaxLength(255);

        builder.Property(s => s.Reason)
            .HasMaxLength(2000);

        builder.HasIndex(s => s.ComputerId)
            .HasDatabaseName("ix_screen_captures_computer_id");

        builder.HasIndex(s => s.CapturedAt)
            .HasDatabaseName("ix_screen_captures_captured_at");
    }
}
