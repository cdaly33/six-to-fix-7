using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class UserPillarProgressConfiguration : IEntityTypeConfiguration<UserPillarProgress>
{
    public void Configure(EntityTypeBuilder<UserPillarProgress> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Pillar)
            .HasConversion<int>();

        builder.HasIndex(e => new { e.TenantId, e.Pillar });

        builder.HasIndex(e => new { e.UserId, e.Pillar })
            .IsUnique();
    }
}
