using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SixToFix.Domain.Entities;

namespace SixToFix.Infrastructure.Data.EntityConfigurations;

public sealed class CategoryResultVersionConfiguration : IEntityTypeConfiguration<CategoryResultVersion>
{
    // Append-only. DELETE operations should be rejected at the DB level (see ADR).
    public void Configure(EntityTypeBuilder<CategoryResultVersion> builder)
    {
        builder.ToTable("category_result_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Action).HasMaxLength(20).IsRequired();
        builder.Property(v => v.ReviewNotes).HasColumnType("text");
        builder.HasIndex(v => v.TenantId);
        builder.HasIndex(v => v.CategoryResultId);
        builder.HasIndex(v => new { v.CategoryResultId, v.Version }).IsUnique();
        builder.Property(v => v.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(v => v.Tenant)
            .WithMany()
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CategoryResult)
            .WithMany(r => r.Versions)
            .HasForeignKey(v => v.CategoryResultId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
