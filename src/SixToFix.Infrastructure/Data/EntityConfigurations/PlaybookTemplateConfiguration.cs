using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Enums;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class PlaybookTemplateConfiguration : IEntityTypeConfiguration<PlaybookTemplate>
{
    public void Configure(EntityTypeBuilder<PlaybookTemplate> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Pillar)
            .HasConversion<int?>();

        builder.Property(e => e.Status)
            .HasConversion<int>();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Format)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(e => e.Notes)
            .HasMaxLength(2048);

        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}
