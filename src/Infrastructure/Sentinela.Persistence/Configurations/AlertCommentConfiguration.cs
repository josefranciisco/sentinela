using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sentinela.Shared.Domain.Alerting;

namespace Sentinela.Persistence.Configurations;

public class AlertCommentConfiguration : IEntityTypeConfiguration<AlertComment>
{
    public void Configure(EntityTypeBuilder<AlertComment> builder)
    {
        builder.ToTable("alert_comments");

        builder.HasKey(c => c.Id);

        builder.Property(e => e.TenantId);

        builder.Property(c => c.Comment)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(c => c.Author)
            .IsRequired()
            .HasMaxLength(255);
    }
}
