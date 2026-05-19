using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Enums;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class PillarContentConfiguration : IEntityTypeConfiguration<PillarContent>
{
    public void Configure(EntityTypeBuilder<PillarContent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Pillar)
            .HasConversion<int>();

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Subtitle)
            .HasMaxLength(512);

        builder.Property(e => e.BodyJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasIndex(e => new { e.TenantId, e.Pillar })
            .IsUnique();
    }
}
