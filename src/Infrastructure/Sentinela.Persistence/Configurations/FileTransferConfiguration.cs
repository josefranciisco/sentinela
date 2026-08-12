using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Persistence.Models;

namespace Sentinela.Persistence.Configurations;

public class FileTransferConfiguration : IEntityTypeConfiguration<FileTransferRecord>
{
    public void Configure(EntityTypeBuilder<FileTransferRecord> builder)
    {
        builder.ToTable("file_transfers");

        builder.HasKey(t => t.Id);

        builder.Property(e => e.TenantId);

        builder.Property(t => t.SessionId)
            .IsRequired();

        builder.Property(t => t.Direction)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.FileSize)
            .IsRequired();

        builder.Property(t => t.BytesTransferred)
            .IsRequired();

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(t => t.SourcePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(t => t.DestinationPath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(t => t.Checksum)
            .HasMaxLength(128);

        builder.Property(t => t.TransferredBy)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.IsEncrypted)
            .IsRequired();

        builder.HasIndex(t => t.SessionId)
            .HasDatabaseName("ix_file_transfers_session_id");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_file_transfers_status");

        builder.HasIndex(t => t.StartedAt)
            .HasDatabaseName("ix_file_transfers_started_at");
    }
}
