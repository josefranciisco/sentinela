using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Monitoring;

namespace Sentinela.Persistence.Configurations;

public class ApplicationUsageConfiguration : IEntityTypeConfiguration<ApplicationUsage>
{
    public void Configure(EntityTypeBuilder<ApplicationUsage> builder)
    {
        builder.ToTable("application_usages");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ProcessName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(a => a.WindowTitle)
            .HasMaxLength(1024);

        builder.Property(a => a.ExecutablePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(a => new { a.ComputerId, a.ProcessName, a.StartTime })
            .HasDatabaseName("ix_application_usages_computer_id_process_name_start_time");
    }
}
