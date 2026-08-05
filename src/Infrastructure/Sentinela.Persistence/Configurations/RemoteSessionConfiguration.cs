using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Persistence.Models;

namespace Sentinela.Persistence.Configurations;

public class RemoteSessionConfiguration : IEntityTypeConfiguration<RemoteSession>
{
    public void Configure(EntityTypeBuilder<RemoteSession> builder)
    {
        builder.ToTable("remote_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ComputerId)
            .IsRequired();

        builder.Property(s => s.RequestedBy)
            .HasMaxLength(255);

        builder.Property(s => s.SessionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.TerminatedBy)
            .HasMaxLength(255);

        builder.Property(s => s.MonitorIndex);

        builder.HasIndex(s => s.ComputerId)
            .HasDatabaseName("ix_remote_sessions_computer_id");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_remote_sessions_status");

        builder.HasIndex(s => s.RequestedAt)
            .HasDatabaseName("ix_remote_sessions_requested_at");
    }
}
